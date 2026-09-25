using Serilog;
using SROServer.GameServer.Cache;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.Shared.Constants;
using SROServer.Shared.Models;
using SROServer.Shared.Packets;

namespace SROServer.GameServer.Services;

/// <summary>
/// Envanterde eşya taşıma sonucu.
/// Not: MovementService içindeki MoveResult sınıfı ile ad çakışmasını önlemek için
/// bu enum ItemMoveResult olarak adlandırılmıştır.
/// </summary>
public enum ItemMoveResult
{
    Success,
    InvalidSlot,
    SlotOccupied,
    NotOwner,
    DatabaseError
}

/// <summary>Eşya kullanma sonucu.</summary>
public enum UseResult
{
    Success,
    NotUsable,
    Cooldown,
    InventoryError
}

/// <summary>Eşya düşürme sonucu.</summary>
public enum DropResult
{
    Success,
    SlotEmpty,
    DatabaseError
}

/// <summary>
/// Envanter iş mantığı: yükleme, taşıma, kullanma, düşürme, ağırlık hesabı ve
/// istemciye gönderilecek paketlerin üretimi. Kalıcılık için InventoryRepository,
/// eşya referans verisi için ItemReferenceCache kullanır.
/// Veritabanı erişilemezse işlemler bellek içi durumda yine de tamamlanır (geliştirme modu).
/// </summary>
public sealed class InventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly ItemReferenceCache _itemCache;

    // İksir kullanımının iyileştirdiği sabit HP/MP miktarı (referans veri yoksa varsayılan).
    private const int DefaultPotionHeal = 200;

    public InventoryService(IInventoryRepository repository, ItemReferenceCache itemCache)
    {
        _repository = repository;
        _itemCache = itemCache;
    }

    /// <summary>
    /// Oyuncunun envanterini veritabanından yükler ve PlayerEntity üzerine yerleştirir.
    /// Veritabanı yoksa boş envanterle döner.
    /// </summary>
    public async Task<List<InventorySlot>> LoadInventoryAsync(PlayerEntity player)
    {
        var slots = new List<InventorySlot>();
        try
        {
            var loaded = await _repository.LoadInventoryAsync(player.CharacterId);
            slots.AddRange(loaded);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Envanter yüklenemedi (veritabanı yok olabilir): {Char}", player.CharName);
        }

        player.Inventory = slots;
        player.CurrentWeight = CalculateTotalWeight(slots);
        return slots;
    }

    /// <summary>
    /// Bir eşyayı kaynak slottan hedef slota taşır. Hedef doluysa SlotOccupied döner.
    /// Bellek içi durum güncellenir, ardından kalıcılık denenir.
    /// </summary>
    public async Task<ItemMoveResult> MoveItemAsync(PlayerEntity player, byte fromSlot, byte toSlot)
    {
        if (fromSlot == toSlot)
            return ItemMoveResult.InvalidSlot;

        InventorySlot? from = FindSlot(player, fromSlot);
        if (from == null || from.IsEmpty)
            return ItemMoveResult.InvalidSlot;

        InventorySlot? to = FindSlot(player, toSlot);
        if (to != null && !to.IsEmpty)
            return ItemMoveResult.SlotOccupied;

        // Bellek içinde taşı.
        from.Slot = toSlot;
        if (from.Item != null)
        {
            // Item nesnesindeki slot bilgisi yok; yalnızca InventorySlot.Slot güncellenir.
        }

        // Kalıcılık (veritabanı yoksa hatayı yut).
        try
        {
            await _repository.MoveItemAsync(player.CharacterId, fromSlot, toSlot);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Eşya taşıma kalıcı hale getirilemedi (Char={Char}).", player.CharName);
        }

        return ItemMoveResult.Success;
    }

    /// <summary>
    /// Bir slottaki eşyayı kullanır. Yalnızca kullanılabilir eşyalar (iksir vb.) işlenir.
    /// İksir HP/MP iyileştirir ve adedi bir azaltır; adet 0 olursa slot boşalır.
    /// </summary>
    public async Task<UseResult> UseItemAsync(PlayerEntity player, byte slot)
    {
        InventorySlot? target = FindSlot(player, slot);
        if (target == null || target.IsEmpty || target.Item == null)
            return UseResult.InventoryError;

        ItemData item = target.Item;
        if (!IsUsable(item))
            return UseResult.NotUsable;

        // İksir etkisi: kod adına göre HP veya MP iyileştir.
        string code = item.CodeName.ToUpperInvariant();
        if (code.Contains("MP"))
            player.MP = Math.Min(player.MaxMP, player.MP + DefaultPotionHeal);
        else
            player.HP = Math.Min(player.MaxHP, player.HP + DefaultPotionHeal);

        // Adedi azalt.
        item.Quantity -= 1;
        try
        {
            if (item.Quantity <= 0)
            {
                RemoveSlot(player, slot);
                await _repository.DeleteItemAsync(item.Id64);
            }
            else
            {
                await _repository.UpdateItemQuantityAsync(item.Id64, item.Quantity);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Eşya kullanımı kalıcı hale getirilemedi (Char={Char}).", player.CharName);
        }

        player.CurrentWeight = CalculateTotalWeight(player.Inventory);
        return UseResult.Success;
    }

    /// <summary>
    /// Bir slottaki eşyayı düşürür (envanterden ve veritabanından siler).
    /// Slot boşsa SlotEmpty döner.
    /// </summary>
    public async Task<DropResult> DropItemAsync(PlayerEntity player, byte slot)
    {
        InventorySlot? target = FindSlot(player, slot);
        if (target == null || target.IsEmpty || target.Item == null)
            return DropResult.SlotEmpty;

        long itemId = target.Item.Id64;
        RemoveSlot(player, slot);

        try
        {
            await _repository.DeleteItemAsync(itemId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Eşya düşürme kalıcı hale getirilemedi (Char={Char}).", player.CharName);
        }

        player.CurrentWeight = CalculateTotalWeight(player.Inventory);
        return DropResult.Success;
    }

    /// <summary>Verilen slotlardaki eşyaların toplam ağırlığını hesaplar (adet dahil).</summary>
    public int CalculateTotalWeight(IEnumerable<InventorySlot> slots)
    {
        int total = 0;
        foreach (InventorySlot slot in slots)
        {
            if (slot.Item != null)
                total += slot.Item.Weight * Math.Max(1, slot.Item.Quantity);
        }
        return total;
    }

    /// <summary>Oyuncu taşıma kapasitesini aştı mı?</summary>
    public bool IsOverweight(PlayerEntity player)
        => CalculateTotalWeight(player.Inventory) > player.MaxWeight;

    // ==================== Paket üreticileri ====================

    /// <summary>
    /// S_INVENTORY_DATA: tüm envanteri istemciye gönderir.
    /// [count(short)] her slot için: [slot(byte)] [itemId(long)] [refItemId(int)]
    /// [name(string)] [optLevel(byte)] [quantity(int)] [typeId1/2/3(int)]
    /// [reqLevel(byte)] [weight(int)].
    /// </summary>
    public byte[] BuildInventoryPacket(List<InventorySlot> slots)
    {
        var filled = slots.Where(s => !s.IsEmpty && s.Item != null).ToList();
        var builder = new PacketBuilder(PacketOpcodes.S_INVENTORY_DATA)
            .WriteShort((ushort)filled.Count);

        foreach (InventorySlot slot in filled)
            WriteItemFields(builder, slot.Slot, slot.Item!);

        return builder.Build();
    }

    /// <summary>
    /// S_INVENTORY_UPDATE: tek bir slotun güncel durumunu gönderir.
    /// [slot(byte)] [hasItem(byte 0/1)] eşya varsa eşya alanları.
    /// </summary>
    public byte[] BuildSlotUpdatePacket(byte slot, ItemData? item)
    {
        var builder = new PacketBuilder(PacketOpcodes.S_INVENTORY_UPDATE)
            .WriteByte(slot)
            .WriteByte((byte)(item != null ? 1 : 0));

        if (item != null)
            WriteItemFields(builder, slot, item, includeSlot: false);

        return builder.Build();
    }

    // ==================== Yardımcılar ====================

    private static void WriteItemFields(PacketBuilder builder, byte slot, ItemData item, bool includeSlot = true)
    {
        if (includeSlot)
            builder.WriteByte(slot);

        builder.WriteLong(item.Id64)
               .WriteInt(item.RefItemId)
               .WriteString(item.Name)
               .WriteByte(item.OptLevel)
               .WriteInt(item.Quantity)
               .WriteInt(item.TypeId1)
               .WriteInt(item.TypeId2)
               .WriteInt(item.TypeId3)
               .WriteByte(item.RequiredLevel)
               .WriteInt(item.Weight);
    }

    /// <summary>Bir eşyanın kullanılabilir (iksir/sarf malzemesi) olup olmadığını belirler.</summary>
    private static bool IsUsable(ItemData item)
    {
        if (!string.IsNullOrEmpty(item.CodeName) &&
            item.CodeName.ToUpperInvariant().Contains("POTION"))
            return true;

        // TypeID1=3 (ETC), TypeID2=3 (sarf) sınıfı sarf malzemesi kabul edilir.
        return item.TypeId1 == 3 && item.TypeId2 == 3;
    }

    private static InventorySlot? FindSlot(PlayerEntity player, byte slot)
        => player.Inventory.FirstOrDefault(s => s.Slot == slot);

    private static void RemoveSlot(PlayerEntity player, byte slot)
    {
        InventorySlot? existing = FindSlot(player, slot);
        if (existing != null)
            player.Inventory.Remove(existing);
    }
}
