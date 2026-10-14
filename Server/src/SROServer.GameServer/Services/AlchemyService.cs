using Serilog;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Services;

/// <summary>
/// Bir eşya güçlendirme (alkimi) denemesinin sonucu.
/// </summary>
public sealed class AlchemyResult
{
    /// <summary>Güçlendirme başarılı oldu mu?</summary>
    public bool Success { get; set; }

    /// <summary>Denemeden sonraki yükseltme (plus) seviyesi.</summary>
    public byte NewOptLevel { get; set; }

    /// <summary>Başarısızlıkta eşya yok mu edildi? (+5 ve üzeri başarısızlıkta).</summary>
    public bool ItemDestroyed { get; set; }

    /// <summary>İşlenen eşyanın veritabanı kimliği (istemciye bildirim için).</summary>
    public long ItemId64 { get; set; }
}

/// <summary>
/// Simya (alkimi) iş mantığı: eşya güçlendirme (plus) denemeleri.
/// Silkroad'dan esinlenen başarı oranları ve yok olma kuralları uygulanır.
/// Kalıcılık için InventoryRepository kullanılır (veritabanı yoksa yutulur).
/// </summary>
public sealed class AlchemyService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly Random _random;

    // Mevcut plus seviyesine göre başarı oranları (index = mevcut seviye).
    // Örn. +0 -> +1 denemesi %80, +11 -> +12 denemesi %3.
    private static readonly double[] SuccessRates =
    {
        0.80, // +0 -> +1
        0.70, // +1 -> +2
        0.60, // +2 -> +3
        0.50, // +3 -> +4
        0.40, // +4 -> +5
        0.30, // +5 -> +6
        0.20, // +6 -> +7
        0.15, // +7 -> +8
        0.10, // +8 -> +9
        0.07, // +9 -> +10
        0.05, // +10 -> +11
        0.03  // +11 -> +12
    };

    // Bu seviyenin üzerindeki başarısız denemeler eşyayı yok eder (+5 ve üzeri).
    private const byte DestroyThreshold = 4;

    // Desteklenen maksimum yükseltme seviyesi.
    private const byte MaxOptLevel = 12;

    public AlchemyService(IInventoryRepository inventoryRepository, Random? random = null)
    {
        _inventoryRepository = inventoryRepository;
        _random = random ?? new Random();
    }

    /// <summary>
    /// Bir eşya için güçlendirme denemesi yapar (kalıcılık yapmaz; yalnızca sonucu hesaplar
    /// ve item nesnesini günceller).
    ///
    /// Kurallar:
    ///   - Başarı oranı, mevcut seviyeye göre azalır (bkz. SuccessRates).
    ///   - "Şanslı" (lucky) taş başarı oranını %10 artırır.
    ///   - Başarıda OptLevel bir artar.
    ///   - +4 ve altında başarısızlıkta eşya korunur; +5 ve üzerinde yok olur.
    /// </summary>
    public AlchemyResult TryEnhance(ItemData item, bool useLucky = false)
    {
        var result = new AlchemyResult
        {
            ItemId64 = item.Id64,
            NewOptLevel = item.OptLevel
        };

        // Zaten maksimum seviyede: başarısız kabul edilir, eşya korunur.
        if (item.OptLevel >= MaxOptLevel)
        {
            result.Success = false;
            return result;
        }

        double rate = SuccessRates[item.OptLevel];
        if (useLucky)
            rate = Math.Min(1.0, rate + 0.10);

        bool success = _random.NextDouble() < rate;
        if (success)
        {
            item.OptLevel += 1;
            result.Success = true;
            result.NewOptLevel = item.OptLevel;
        }
        else
        {
            result.Success = false;
            // +5 ve üzeri denemelerde (mevcut seviye > 4) başarısızlık eşyayı yok eder.
            if (item.OptLevel > DestroyThreshold)
                result.ItemDestroyed = true;
        }

        return result;
    }

    /// <summary>
    /// Bir oyuncunun envanterindeki eşyayı, güçlendirme taşı kullanarak güçlendirir.
    /// Sonuca göre eşyayı günceller veya (yok olduysa) envanterden siler.
    /// </summary>
    public async Task<AlchemyResult> ProcessEnhanceAsync(PlayerEntity player, byte itemSlot, byte stoneSlot)
    {
        InventorySlot? itemSlotObj = player.Inventory.FirstOrDefault(s => s.Slot == itemSlot);
        InventorySlot? stoneSlotObj = player.Inventory.FirstOrDefault(s => s.Slot == stoneSlot);

        // Geçerli eşya ve taş olmalı.
        if (itemSlotObj?.Item == null || stoneSlotObj?.Item == null)
        {
            return new AlchemyResult { Success = false, NewOptLevel = 0 };
        }

        ItemData item = itemSlotObj.Item;
        AlchemyResult result = TryEnhance(item);

        try
        {
            // Güçlendirme taşını bir adet tüket.
            stoneSlotObj.Item.Quantity -= 1;
            if (stoneSlotObj.Item.Quantity <= 0)
            {
                long stoneId = stoneSlotObj.Item.Id64;
                player.Inventory.Remove(stoneSlotObj);
                await _inventoryRepository.DeleteItemAsync(stoneId);
            }
            else
            {
                await _inventoryRepository.UpdateItemQuantityAsync(
                    stoneSlotObj.Item.Id64, stoneSlotObj.Item.Quantity);
            }

            if (result.ItemDestroyed)
            {
                long itemId = item.Id64;
                player.Inventory.Remove(itemSlotObj);
                await _inventoryRepository.DeleteItemAsync(itemId);
            }
            // Başarı/başarısızlık durumunda OptLevel'ı kalıcı yapmak için (gerçek şemada
            // _Items.OptLevel güncellenir) burada adet güncellemesi yeterlidir; OptLevel
            // kalıcılığı repository genişletildiğinde eklenecektir.
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Güçlendirme kalıcı hale getirilemedi (Char={Char}).", player.CharName);
        }

        Log.Debug("Güçlendirme: {Char} slot={Slot} başarı={Success} yeniSeviye=+{Level} yokOldu={Destroyed}",
            player.CharName, itemSlot, result.Success, result.NewOptLevel, result.ItemDestroyed);

        return result;
    }
}
