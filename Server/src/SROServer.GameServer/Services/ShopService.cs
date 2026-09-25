using Serilog;
using SROServer.GameServer.Cache;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Services;

/// <summary>Eşya satın alma sonucu.</summary>
public enum BuyResult
{
    Success,
    NotEnoughGold,
    InventoryFull,
    InvalidItem,
    DatabaseError
}

/// <summary>Eşya satma sonucu.</summary>
public enum SellResult
{
    Success,
    SlotEmpty,
    DatabaseError
}

/// <summary>
/// NPC dükkanı iş mantığı: eşya satın alma ve satma. Fiyat/ağırlık gibi statik
/// veriler için ItemReferenceCache, kalıcılık için InventoryRepository kullanır.
/// Satış fiyatı, temel fiyatın yarısıdır (Silkroad benzeri).
/// </summary>
public sealed class ShopService
{
    private readonly IInventoryRepository _repository;
    private readonly ItemReferenceCache _itemCache;

    // Envanter çanta bölümünün maksimum slot sayısı (basit sınır).
    private const int MaxInventorySlots = 109;

    public ShopService(IInventoryRepository repository, ItemReferenceCache itemCache)
    {
        _repository = repository;
        _itemCache = itemCache;
    }

    /// <summary>
    /// Bir referans eşyasını satın alır: altını kontrol eder, düşer ve eşyayı hedef slota
    /// (veya ilk boş slota) ekler.
    /// </summary>
    public async Task<BuyResult> BuyItemAsync(PlayerEntity player, int refItemId, byte targetSlot)
    {
        ItemReference? reference = _itemCache.GetItem(refItemId);
        if (reference == null)
            return BuyResult.InvalidItem;

        if (player.Gold < reference.Price)
            return BuyResult.NotEnoughGold;

        byte slot = targetSlot;
        if (IsSlotOccupied(player, slot) || slot == 0)
        {
            int free = FindFreeSlot(player);
            if (free < 0)
                return BuyResult.InventoryFull;
            slot = (byte)free;
        }

        player.Gold -= reference.Price;

        long newItemId = 0;
        try
        {
            newItemId = await _repository.AddItemAsync(player.CharacterId, slot, refItemId, 1);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Satın alma kalıcı hale getirilemedi (Char={Char}).", player.CharName);
        }

        // Bellek içi envantere ekle.
        var item = new ItemData
        {
            Id64 = newItemId,
            RefItemId = refItemId,
            CodeName = reference.CodeName,
            Name = string.IsNullOrEmpty(reference.Name) ? reference.CodeName : reference.Name,
            TypeId1 = reference.TypeId1,
            TypeId2 = reference.TypeId2,
            TypeId3 = reference.TypeId3,
            MaxStack = reference.MaxStack,
            Durability = reference.Durability,
            Price = reference.Price,
            RequiredLevel = reference.ReqLevel,
            Weight = reference.Weight,
            Quantity = 1
        };
        player.Inventory.Add(new InventorySlot { Slot = slot, ItemId = newItemId, Item = item });

        return BuyResult.Success;
    }

    /// <summary>
    /// Bir slottaki eşyayı satar: eşyayı siler ve satış fiyatını (temel fiyatın yarısı)
    /// oyuncunun altınına ekler.
    /// </summary>
    public async Task<SellResult> SellItemAsync(PlayerEntity player, byte slot)
    {
        InventorySlot? target = player.Inventory.FirstOrDefault(s => s.Slot == slot);
        if (target == null || target.IsEmpty || target.Item == null)
            return SellResult.SlotEmpty;

        ItemData item = target.Item;
        long sellPrice = Math.Max(0, item.Price / 2) * Math.Max(1, item.Quantity);

        player.Inventory.Remove(target);
        player.Gold += sellPrice;

        try
        {
            await _repository.DeleteItemAsync(item.Id64);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Satma kalıcı hale getirilemedi (Char={Char}).", player.CharName);
        }

        return SellResult.Success;
    }

    // ==================== Yardımcılar ====================

    private static bool IsSlotOccupied(PlayerEntity player, byte slot)
        => player.Inventory.Any(s => s.Slot == slot && !s.IsEmpty);

    private static int FindFreeSlot(PlayerEntity player)
    {
        // Slot 0-12 ekipman kabul edilir; çanta 13'ten başlar (basitleştirilmiş).
        for (int i = 13; i < MaxInventorySlots; i++)
        {
            if (!player.Inventory.Any(s => s.Slot == i))
                return i;
        }
        return -1;
    }
}
