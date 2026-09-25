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

    public GamePacketHandler(
        WorldManager world,
        MovementService movementService,
        InventoryService inventoryService,
        ShopService shopService)
    {
        _world = world;
        _movementService = movementService;
        _inventoryService = inventoryService;
        _shopService = shopService;
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

    // ==================== Paket üreticileri ====================

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
