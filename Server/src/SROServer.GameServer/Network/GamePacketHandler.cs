using LiteNetLib;
using Serilog;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Services;
using SROServer.GameServer.World;
using SROServer.Shared.Constants;
using SROServer.Shared.Models;
using SROServer.Shared.Packets;

namespace SROServer.GameServer.Network;

/// <summary>
/// Oyun (game) paketlerini işleyen ve yayınlanacak paketleri üreten sınıf.
/// Hareket, durma ve sektör değişimi isteklerini işler; spawn/despawn/move/init
/// paketlerini oluşturur.
/// </summary>
public sealed class GamePacketHandler
{
    private readonly WorldManager _world;
    private readonly MovementService _movementService;
    private readonly InventoryService _inventoryService;
    private readonly ShopService _shopService;
    private readonly CombatService _combatService;
    private readonly SkillService _skillService;
    private readonly AlchemyService _alchemyService;
    private readonly PartyService _partyService;
    private readonly GuildService _guildService;
    private readonly SpawnService _spawnService;

    public GamePacketHandler(
        WorldManager world,
        MovementService movementService,
        InventoryService inventoryService,
        ShopService shopService,
        CombatService combatService,
        SkillService skillService,
        AlchemyService alchemyService,
        PartyService partyService,
        GuildService guildService,
        SpawnService spawnService)
    {
        _world = world;
        _movementService = movementService;
        _inventoryService = inventoryService;
        _shopService = shopService;
        _combatService = combatService;
        _skillService = skillService;
        _alchemyService = alchemyService;
        _partyService = partyService;
        _guildService = guildService;
        _spawnService = spawnService;
    }

    // ==================== Gelen istek işleyicileri ====================

    /// <summary>
    /// C_MOVE_REQUEST: oyuncunun hedef pozisyonunu okur, doğrular ve kabul edilirse
    /// bölgedeki diğer oyunculara S_ENTITY_MOVE yayınlar.
    /// </summary>
    public async Task HandleMoveRequestAsync(PlayerEntity player, PacketReader reader)
    {
        float x = reader.ReadFloat();
        float y = reader.ReadFloat();
        float z = reader.ReadFloat();
        var target = new SROVector3(x, y, z);

        MoveResult result = await _movementService.ProcessMoveRequestAsync(player, target);
        if (!result.Accepted)
        {
            // Reddedilirse oyuncuyu mevcut (otoriter) pozisyonuna geri sabitle.
            byte[] correction = BuildEntityMovePacket(player, player.Position);
            Send(player.Connection, correction);
            return;
        }

        // Bölge değiştiyse istemciye onay gönder.
        if (result.RegionChanged)
        {
            byte[] ack = new PacketBuilder(PacketOpcodes.S_SECTOR_ACK)
                .WriteShort((ushort)player.RegionId)
                .Build();
            Send(player.Connection, ack);
        }

        // Bölgedeki diğer oyunculara hareketi yayınla.
        byte[] movePacket = BuildEntityMovePacket(player, result.TargetPosition);
        BroadcastToRegion(player.RegionId, movePacket, player.Connection);

        // Pozisyonu throttle'lı kaydet.
        await _movementService.ThrottleSavePositionAsync(player);
    }

    /// <summary>
    /// C_STOP_REQUEST: oyuncu durur; bölgedeki diğer oyunculara S_ENTITY_STOP yayınlanır.
    /// </summary>
    public void HandleStopRequest(PlayerEntity player, PacketReader reader)
    {
        float x = reader.ReadFloat();
        float y = reader.ReadFloat();
        float z = reader.ReadFloat();

        player.Position = new SROVector3(x, y, z);
        player.IsMoving = false;
        player.State = EntityState.Idle;

        byte[] stopPacket = new PacketBuilder(PacketOpcodes.S_ENTITY_STOP)
            .WriteInt((int)player.UniqueId)
            .WriteFloat(x).WriteFloat(y).WriteFloat(z)
            .Build();
        BroadcastToRegion(player.RegionId, stopPacket, player.Connection);
    }

    /// <summary>
    /// C_SECTOR_CHANGE: istemcinin bildirdiği sektör değişimini işler ve onay gönderir.
    /// (Otoriter sektör hesabı hareketle yapılır; bu, istemci-taraflı senkron bilgisidir.)
    /// </summary>
    public void HandleSectorChange(PlayerEntity player, PacketReader reader)
    {
        ushort regionId = reader.ReadShort();

        byte[] ack = new PacketBuilder(PacketOpcodes.S_SECTOR_ACK)
            .WriteShort(regionId)
            .Build();
        Send(player.Connection, ack);

        Log.Debug("Sektör değişim bildirimi: {Char} -> Region={Region}",
            player.CharName, regionId);
    }

    // ==================== Faz 3: Envanter & Item işleyicileri ====================

    /// <summary>
    /// C_INVENTORY_MOVE: [fromSlot(byte)] [toSlot(byte)]. Eşyayı taşır ve iki slotun
    /// güncel durumunu S_INVENTORY_UPDATE ile geri gönderir.
    /// </summary>
    public async Task HandleInventoryMoveAsync(PlayerEntity player, PacketReader reader)
    {
        byte fromSlot = reader.ReadByte();
        byte toSlot = reader.ReadByte();

        ItemMoveResult result = await _inventoryService.MoveItemAsync(player, fromSlot, toSlot);
        if (result == ItemMoveResult.Success)
        {
            // Kaynak artık boş, hedefte eşya var.
            ItemData? moved = player.Inventory.FirstOrDefault(s => s.Slot == toSlot)?.Item;
            Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(fromSlot, null));
            Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(toSlot, moved));
        }
        else
        {
            Log.Debug("Envanter taşıma reddedildi: {Char} {From}->{To} ({Result})",
                player.CharName, fromSlot, toSlot, result);
            // Reddedilirse mevcut envanteri yeniden gönder (istemci senkronu).
            Send(player.Connection, _inventoryService.BuildInventoryPacket(player.Inventory));
        }
    }

    /// <summary>
    /// C_ITEM_USE: [slot(byte)]. Eşyayı kullanır; sonucu ve güncel slot durumunu gönderir.
    /// </summary>
    public async Task HandleItemUseAsync(PlayerEntity player, PacketReader reader)
    {
        byte slot = reader.ReadByte();
        UseResult result = await _inventoryService.UseItemAsync(player, slot);

        byte[] resultPacket = new PacketBuilder(PacketOpcodes.S_ITEM_USE_RESULT)
            .WriteByte(slot)
            .WriteByte((byte)result)
            .WriteInt(player.HP)
            .WriteInt(player.MP)
            .Build();
        Send(player.Connection, resultPacket);

        if (result == UseResult.Success)
        {
            ItemData? item = player.Inventory.FirstOrDefault(s => s.Slot == slot)?.Item;
            Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(slot, item));
        }
    }

    /// <summary>
    /// C_ITEM_DROP: [slot(byte)]. Eşyayı düşürür (siler); sonucu ve slot durumunu gönderir.
    /// </summary>
    public async Task HandleItemDropAsync(PlayerEntity player, PacketReader reader)
    {
        byte slot = reader.ReadByte();
        DropResult result = await _inventoryService.DropItemAsync(player, slot);

        byte[] resultPacket = new PacketBuilder(PacketOpcodes.S_ITEM_DROP_RESULT)
            .WriteByte(slot)
            .WriteByte((byte)result)
            .Build();
        Send(player.Connection, resultPacket);

        if (result == DropResult.Success)
            Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(slot, null));
    }

    /// <summary>
    /// C_SHOP_BUY: [refItemId(int)] [targetSlot(byte)]. Eşyayı satın alır; sonucu,
    /// güncel altını ve yeni slot durumunu gönderir.
    /// </summary>
    public async Task HandleShopBuyAsync(PlayerEntity player, PacketReader reader)
    {
        int refItemId = reader.ReadInt();
        byte targetSlot = reader.ReadByte();

        BuyResult result = await _shopService.BuyItemAsync(player, refItemId, targetSlot);

        byte[] resultPacket = new PacketBuilder(PacketOpcodes.S_SHOP_BUY_RESULT)
            .WriteByte((byte)result)
            .WriteLong(player.Gold)
            .Build();
        Send(player.Connection, resultPacket);

        if (result == BuyResult.Success)
        {
            InventorySlot? added = player.Inventory.LastOrDefault(s => s.Item?.RefItemId == refItemId);
            if (added != null)
                Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(added.Slot, added.Item));
        }
    }

    /// <summary>
    /// C_SHOP_SELL: [slot(byte)]. Eşyayı satar; sonucu, güncel altını ve boş slotu gönderir.
    /// </summary>
    public async Task HandleShopSellAsync(PlayerEntity player, PacketReader reader)
    {
        byte slot = reader.ReadByte();
        SellResult result = await _shopService.SellItemAsync(player, slot);

        byte[] resultPacket = new PacketBuilder(PacketOpcodes.S_SHOP_SELL_RESULT)
            .WriteByte(slot)
            .WriteByte((byte)result)
            .WriteLong(player.Gold)
            .Build();
        Send(player.Connection, resultPacket);

        if (result == SellResult.Success)
            Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(slot, null));
    }

    /// <summary>
    /// Oyuncu dünyaya girdiğinde envanterini yükler ve S_INVENTORY_DATA gönderir.
    /// </summary>
    public async Task SendInventoryOnJoinAsync(PlayerEntity player)
    {
        List<InventorySlot> slots = await _inventoryService.LoadInventoryAsync(player);
        Send(player.Connection, _inventoryService.BuildInventoryPacket(slots));
    }

    // ==================== Faz 4: Savaş & Skill işleyicileri ====================

    /// <summary>
    /// C_ATTACK_REQUEST: [targetId(int)]. Hedefe temel saldırı uygular; sonucu bölgeye
    /// yayınlar ve hedef öldüyse S_ENTITY_DEATH gönderip (canavarsa) respawn zamanlar.
    /// </summary>
    public async Task HandleAttackRequestAsync(PlayerEntity player, PacketReader reader)
    {
        uint targetId = (uint)reader.ReadInt();
        EntityBase? target = _world.GetEntity(targetId);
        if (target == null || target.State == EntityState.Dead)
            return;

        player.TargetEntityId = targetId;
        CombatResult result = await _combatService.ProcessAttackAsync(player, target);

        byte[] attackPacket = BuildAttackResultPacket(result);
        BroadcastToRegion(player.RegionId, attackPacket, null);

        if (result.IsTargetDead)
            await HandleEntityDeathAsync(target);
    }

    /// <summary>
    /// C_SKILL_USE: [skillId(int)] [targetId(int)]. Yeteneği doğrular/uygular; sonucu
    /// oyuncuya, (başarılıysa) yetenek yayınını bölgeye gönderir.
    /// </summary>
    public async Task HandleSkillUseAsync(PlayerEntity player, PacketReader reader)
    {
        int skillId = reader.ReadInt();
        uint targetId = (uint)reader.ReadInt();

        SkillUseResult result = await _skillService.UseSkillAsync(player, skillId, targetId);

        byte[] resultPacket = new PacketBuilder(PacketOpcodes.S_SKILL_RESULT)
            .WriteInt(skillId)
            .WriteByte((byte)result)
            .WriteInt(player.MP)
            .Build();
        Send(player.Connection, resultPacket);

        if (result == SkillUseResult.Success)
        {
            byte[] castPacket = new PacketBuilder(PacketOpcodes.S_SKILL_CAST)
                .WriteInt((int)player.UniqueId)
                .WriteInt(skillId)
                .WriteInt((int)targetId)
                .Build();
            BroadcastToRegion(player.RegionId, castPacket, null);

            // Hedef öldüyse ölüm bildir.
            EntityBase? target = _world.GetEntity(targetId);
            if (target != null && target.State == EntityState.Dead)
                await HandleEntityDeathAsync(target);
        }
    }

    /// <summary>
    /// C_RESURRECTION: oyuncuyu kısmi HP/MP ile diriltir ve durumunu günceller.
    /// </summary>
    public void HandleResurrection(PlayerEntity player, PacketReader reader)
    {
        if (player.State != EntityState.Dead)
            return;

        player.HP = Math.Max(1, player.MaxHP / 2);
        player.MP = Math.Max(1, player.MaxMP / 2);
        player.State = EntityState.Idle;
        player.IsInCombat = false;

        // Güncel canlılığı istemciye bildir.
        byte[] hpPacket = new PacketBuilder(PacketOpcodes.S_HP_UPDATE)
            .WriteInt((int)player.UniqueId)
            .WriteInt(player.HP)
            .WriteInt(player.MaxHP)
            .Build();
        Send(player.Connection, hpPacket);

        Log.Debug("Oyuncu dirildi: {Char}", player.CharName);
    }

    /// <summary>
    /// Bir varlığın ölümünü bölgeye bildirir; canavar ise bölgeden kaldırıp respawn zamanlar.
    /// </summary>
    private async Task HandleEntityDeathAsync(EntityBase target)
    {
        byte[] deathPacket = new PacketBuilder(PacketOpcodes.S_ENTITY_DEATH)
            .WriteInt((int)target.UniqueId)
            .WriteByte((byte)target.Type)
            .Build();
        BroadcastToRegion(target.RegionId, deathPacket, null);

        if (target is MonsterEntity monster)
        {
            Region? region = _world.GetRegion(monster.RegionId);
            region?.OnEntityLeave(monster);
            await _spawnService.ScheduleRespawnAsync(monster, monster.RespawnDelaySeconds);
        }
    }

    // ==================== Faz 5: Alkimi, Party, Guild işleyicileri ====================

    /// <summary>
    /// C_ALCHEMY_REQUEST: [itemSlot(byte)] [stoneSlot(byte)]. Eşyayı güçlendirir ve
    /// S_ALCHEMY_RESULT ile sonucu gönderir.
    /// </summary>
    public async Task HandleAlchemyRequestAsync(PlayerEntity player, PacketReader reader)
    {
        byte itemSlot = reader.ReadByte();
        byte stoneSlot = reader.ReadByte();

        AlchemyResult result = await _alchemyService.ProcessEnhanceAsync(player, itemSlot, stoneSlot);

        byte[] packet = new PacketBuilder(PacketOpcodes.S_ALCHEMY_RESULT)
            .WriteLong(result.ItemId64)
            .WriteByte((byte)(result.Success ? 1 : 0))
            .WriteByte(result.NewOptLevel)
            .WriteByte((byte)(result.ItemDestroyed ? 1 : 0))
            .Build();
        Send(player.Connection, packet);

        // Güçlendirilen/yok edilen slotu senkronla.
        ItemData? item = player.Inventory.FirstOrDefault(s => s.Slot == itemSlot)?.Item;
        Send(player.Connection, _inventoryService.BuildSlotUpdatePacket(itemSlot, item));
    }

    /// <summary>
    /// C_PARTY_CREATE: oyuncu için yeni bir grup oluşturur ve S_PARTY_DATA gönderir.
    /// </summary>
    public void HandlePartyCreate(PlayerEntity player, PacketReader reader)
    {
        Party party = _partyService.CreateParty(player);
        _partyService.BroadcastToParty(party, BuildPartyDataPacket(party));
    }

    /// <summary>
    /// C_PARTY_INVITE: [targetId(int)]. Hedefi davet eder (basitleştirilmiş: anında katılım)
    /// ve gruba güncel veriyi yayınlar.
    /// </summary>
    public void HandlePartyInvite(PlayerEntity player, PacketReader reader)
    {
        uint targetId = (uint)reader.ReadInt();
        PlayerEntity? target = _world.GetPlayer(targetId);

        Party? party = _partyService.GetPlayerParty(player.UniqueId)
                       ?? _partyService.CreateParty(player);

        if (target != null && _partyService.InvitePlayer(party, target))
            _partyService.BroadcastToParty(party, BuildPartyDataPacket(party));
    }

    /// <summary>
    /// C_GUILD_CREATE: [guildName(string)]. Yeni lonca oluşturur ve S_GUILD_DATA gönderir.
    /// </summary>
    public async Task HandleGuildCreateAsync(PlayerEntity player, PacketReader reader)
    {
        string guildName = reader.ReadString();
        Guild guild = await _guildService.CreateGuildAsync(player, guildName);
        Send(player.Connection, BuildGuildDataPacket(guild));
    }

    // ==================== Paket üreticileri ====================

    /// <summary>
    /// S_ATTACK_RESULT: [attackerId(int)] [targetId(int)] [damage(int)]
    /// [isCritical(byte)] [isTargetDead(byte)].
    /// </summary>
    public byte[] BuildAttackResultPacket(CombatResult result)
    {
        return new PacketBuilder(PacketOpcodes.S_ATTACK_RESULT)
            .WriteInt((int)result.AttackerId)
            .WriteInt((int)result.TargetId)
            .WriteInt(result.Damage)
            .WriteByte((byte)(result.IsCritical ? 1 : 0))
            .WriteByte((byte)(result.IsTargetDead ? 1 : 0))
            .Build();
    }

    /// <summary>
    /// S_PARTY_DATA: [partyId(int)] [memberCount(byte)] her üye için:
    /// [uniqueId(int)] [name(string)] [level(byte)] [hp(int)] [maxHp(int)] [mp(int)] [maxMp(int)].
    /// </summary>
    public byte[] BuildPartyDataPacket(Party party)
    {
        var builder = new PacketBuilder(PacketOpcodes.S_PARTY_DATA)
            .WriteInt(party.Id)
            .WriteByte((byte)party.Members.Count);

        foreach (PlayerEntity m in party.Members)
        {
            builder.WriteInt((int)m.UniqueId)
                   .WriteString(m.CharName)
                   .WriteByte(m.Level)
                   .WriteInt(m.HP)
                   .WriteInt(m.MaxHP)
                   .WriteInt(m.MP)
                   .WriteInt(m.MaxMP);
        }

        return builder.Build();
    }

    /// <summary>
    /// S_GUILD_DATA: [guildId(int)] [guildName(string)] [level(byte)] [memberCount(byte)]
    /// her üye için: [name(string)] [rank(byte)].
    /// </summary>
    public byte[] BuildGuildDataPacket(Guild guild)
    {
        var builder = new PacketBuilder(PacketOpcodes.S_GUILD_DATA)
            .WriteInt(guild.Id)
            .WriteString(guild.Name)
            .WriteByte((byte)guild.Level)
            .WriteByte((byte)guild.Members.Count);

        foreach (GuildMember m in guild.Members)
            builder.WriteString(m.CharName).WriteByte(m.Rank);

        return builder.Build();
    }

    // ==================== Paket üreticileri (Faz 2) ====================

    /// <summary>
    /// S_ENTITY_MOVE: [uniqueId(int)] [x,y,z(float)] [angle(float)].
    /// </summary>
    public byte[] BuildEntityMovePacket(EntityBase entity, SROVector3 target)
    {
        return new PacketBuilder(PacketOpcodes.S_ENTITY_MOVE)
            .WriteInt((int)entity.UniqueId)
            .WriteFloat(target.X)
            .WriteFloat(target.Y)
            .WriteFloat(target.Z)
            .WriteFloat(entity.Angle)
            .Build();
    }

    /// <summary>
    /// S_ENTITY_SPAWN: bir varlığın dünyaya girişini bildirir.
    /// [type(byte)] [uniqueId(int)] [x,y,z(float)] [angle(float)] [name(string)]
    /// [level(byte)] [hp(int)] [maxHp(int)].
    /// </summary>
    public byte[] BuildEntitySpawnPacket(EntityBase entity)
    {
        var builder = new PacketBuilder(PacketOpcodes.S_ENTITY_SPAWN)
            .WriteByte((byte)entity.Type)
            .WriteInt((int)entity.UniqueId)
            .WriteFloat(entity.Position.X)
            .WriteFloat(entity.Position.Y)
            .WriteFloat(entity.Position.Z)
            .WriteFloat(entity.Angle);

        switch (entity)
        {
            case PlayerEntity p:
                builder.WriteString(p.CharName)
                       .WriteByte(p.Level)
                       .WriteInt(p.HP)
                       .WriteInt(p.MaxHP);
                break;

            case MonsterEntity m:
                builder.WriteString(m.CodeName)
                       .WriteByte(0)          // canavar seviyesi (şimdilik 0)
                       .WriteInt(m.HP)
                       .WriteInt(m.MaxHP);
                break;

            default:
                builder.WriteString(string.Empty)
                       .WriteByte(0)
                       .WriteInt(0)
                       .WriteInt(0);
                break;
        }

        return builder.Build();
    }

    /// <summary>
    /// S_ENTITY_DESPAWN: [uniqueId(int)].
    /// </summary>
    public byte[] BuildEntityDespawnPacket(uint uniqueId)
    {
        return new PacketBuilder(PacketOpcodes.S_ENTITY_DESPAWN)
            .WriteInt((int)uniqueId)
            .Build();
    }

    /// <summary>
    /// S_INIT_DATA: oyuncunun kendi dünyaya giriş verisi.
    /// [uniqueId(int)] [charName(string)] [level(byte)] [hp,maxHp,mp,maxMp(int)]
    /// [x,y,z(float)] [regionId(short)].
    /// </summary>
    public byte[] BuildInitDataPacket(PlayerEntity player)
    {
        return new PacketBuilder(PacketOpcodes.S_INIT_DATA)
            .WriteInt((int)player.UniqueId)
            .WriteString(player.CharName)
            .WriteByte(player.Level)
            .WriteInt(player.HP)
            .WriteInt(player.MaxHP)
            .WriteInt(player.MP)
            .WriteInt(player.MaxMP)
            .WriteFloat(player.Position.X)
            .WriteFloat(player.Position.Y)
            .WriteFloat(player.Position.Z)
            .WriteShort((ushort)player.RegionId)
            .Build();
    }

    // ==================== Yardımcılar ====================

    /// <summary>Bir bölgedeki oyunculara paket yayınlar.</summary>
    private void BroadcastToRegion(short regionId, byte[] packet, NetPeer? exclude)
    {
        Region? region = _world.GetRegion(regionId);
        region?.BroadcastToPlayers(packet, exclude);
    }

    /// <summary>Tek bir bağlantıya paket gönderir (bağlantı geçerliyse).</summary>
    private static void Send(NetPeer? peer, byte[] data)
    {
        if (peer != null && peer.ConnectionState == ConnectionState.Connected)
            peer.Send(data, DeliveryMethod.ReliableOrdered);
    }
}
