using System;
using Moq;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.Shared.Models;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// AlchemyService iş mantığı testleri (güçlendirme başarı oranı, maksimum seviye,
/// başarısızlıkta eşya korunması/yok olması). RNG belirlenimci olması için sabit
/// tohumlu Random enjekte edilir; repository sahte (mock) olduğundan veritabanı gerekmez.
/// </summary>
public sealed class AlchemyServiceTests
{
    private static Mock<IInventoryRepository> MakeRepo() => new();

    private static ItemData MakeItem(byte optLevel)
    {
        return new ItemData
        {
            Id64 = 500,
            RefItemId = 1000,
            CodeName = "ITEM_TEST_WEAPON",
            Name = "Test Weapon",
            OptLevel = optLevel,
            Quantity = 1
        };
    }

    [Fact]
    public void TryEnhance_Plus0Item_HighSuccessRate()
    {
        // +0 -> +1 oranı %80. seed=1 ilk NextDouble=0.2487 < 0.80 -> basari.
        var service = new AlchemyService(MakeRepo().Object, new Random(1));
        ItemData item = MakeItem(optLevel: 0);

        AlchemyResult result = service.TryEnhance(item);

        Assert.True(result.Success);
        Assert.Equal((byte)1, result.NewOptLevel);
        Assert.Equal((byte)1, item.OptLevel);
        Assert.False(result.ItemDestroyed);
    }

    [Fact]
    public void TryEnhance_Plus12Item_VeryLowSuccessRate()
    {
        // +12 maksimum seviyedir; deneme her zaman basarisiz ve esya korunur.
        var service = new AlchemyService(MakeRepo().Object, new Random(1));
        ItemData item = MakeItem(optLevel: 12);

        AlchemyResult result = service.TryEnhance(item);

        Assert.False(result.Success);
        Assert.Equal((byte)12, result.NewOptLevel);
        Assert.False(result.ItemDestroyed);
    }

    [Fact]
    public void TryEnhance_Failure_ItemPreservedBelow5()
    {
        // +3 -> +4 orani %50. seed=4 ilk NextDouble=0.8159 >= 0.50 -> basarisiz.
        // Mevcut seviye 3 (<= 4 esigi) oldugundan esya yok olmaz.
        var service = new AlchemyService(MakeRepo().Object, new Random(4));
        ItemData item = MakeItem(optLevel: 3);

        AlchemyResult result = service.TryEnhance(item);

        Assert.False(result.Success);
        Assert.False(result.ItemDestroyed);
        Assert.Equal((byte)3, item.OptLevel); // Seviye degismedi.
    }
}
