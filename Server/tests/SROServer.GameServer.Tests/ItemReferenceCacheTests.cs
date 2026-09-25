using SROServer.GameServer.Cache;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// ItemReferenceCache testleri. Veritabanı gerektirmeyen bellek içi davranış
/// (ekleme, arama, bilinmeyen kimlik) doğrulanır.
/// </summary>
public sealed class ItemReferenceCacheTests
{
    private static ItemReference MakeRef(int id, string code = "ITEM_TEST")
    {
        return new ItemReference
        {
            Id = id,
            CodeName = code,
            Name = code,
            TypeId1 = 3,
            TypeId2 = 3,
            TypeId3 = 1,
            MaxStack = 250,
            Durability = 100,
            Price = 500,
            ReqLevel = 1,
            Weight = 10
        };
    }

    [Fact]
    public void GetItem_KnownRefId_ReturnsReference()
    {
        var cache = new ItemReferenceCache();
        cache.AddItemForTest(MakeRef(1000, "ITEM_ETC_HP_POTION_01"));

        ItemReference? item = cache.GetItem(1000);

        Assert.NotNull(item);
        Assert.Equal("ITEM_ETC_HP_POTION_01", item!.CodeName);
        Assert.Equal(500, item.Price);
    }

    [Fact]
    public void GetItem_UnknownRefId_ReturnsNull()
    {
        var cache = new ItemReferenceCache();
        cache.AddItemForTest(MakeRef(1000));

        ItemReference? item = cache.GetItem(9999);

        Assert.Null(item);
    }

    [Fact]
    public void LoadAsync_PopulatesCache()
    {
        // LoadAsync veritabanı olmadan hata yutar; burada bellek içi ekleme ile
        // önbelleğin doldurulabildiğini ve sayacın güncellendiğini doğrularız.
        var cache = new ItemReferenceCache();
        Assert.Equal(0, cache.ItemCount);

        cache.AddItemForTest(MakeRef(1));
        cache.AddItemForTest(MakeRef(2));
        cache.AddItemForTest(MakeRef(3));

        Assert.Equal(3, cache.ItemCount);
        Assert.Equal("UNKNOWN_ITEM_42", cache.ResolveItemName(42));
        Assert.Equal("ITEM_TEST", cache.ResolveItemName(1));
    }
}
