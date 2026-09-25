using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Serilog;

namespace SROServer.GameServer.Cache;

/// <summary>
/// Bir eşyanın statik (referans) verisi (_RefObjCommon + _RefObjItem birleşimi).
/// Tüm örnekler için ortaktır; bir kez yüklenip bellekte tutulur.
/// </summary>
public sealed class ItemReference
{
    public int Id { get; set; }
    public string CodeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TypeId1 { get; set; }
    public int TypeId2 { get; set; }
    public int TypeId3 { get; set; }
    public int MaxStack { get; set; }
    public int Durability { get; set; }
    public int Price { get; set; }
    public byte ReqLevel { get; set; }
    public int Weight { get; set; }
}

/// <summary>
/// Bir büyü seçeneğinin (magic option) referans verisi (_RefMagicOpt).
/// </summary>
public sealed class MagicOptReference
{
    public int Id { get; set; }
    public string CodeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Param1 { get; set; }
    public long Param2 { get; set; }
}

/// <summary>
/// Eşya ve büyü seçeneği referans verilerini bellekte tutan iş parçacığı güvenli
/// singleton önbellek. Sunucu açılışında veritabanından bir kez yüklenir ve
/// tüm servisler tarafından paylaşılır.
/// </summary>
public sealed class ItemReferenceCache
{
    private static readonly Lazy<ItemReferenceCache> _instance =
        new(() => new ItemReferenceCache());

    /// <summary>Tekil (singleton) örnek.</summary>
    public static ItemReferenceCache Instance => _instance.Value;

    private readonly ConcurrentDictionary<int, ItemReference> _items = new();
    private readonly ConcurrentDictionary<int, MagicOptReference> _magicOpts = new();

    /// <summary>Önbellekteki eşya referansı sayısı.</summary>
    public int ItemCount => _items.Count;

    /// <summary>Önbellekteki büyü seçeneği sayısı.</summary>
    public int MagicOptCount => _magicOpts.Count;

    // Test ve elle kullanım için de örneklenebilir olsun.
    public ItemReferenceCache() { }

    /// <summary>
    /// Referans tablolarını (_RefObjCommon + _RefObjItem, _RefMagicOpt) yükler.
    /// Veritabanı erişilemezse hata loglanır ve önbellek boş kalır (sunucu yine başlar).
    /// </summary>
    public async Task LoadAsync(IDbConnection connection)
    {
        try
        {
            const string itemSql = @"
                SELECT c.ID          AS Id,
                       c.CodeName128  AS CodeName,
                       c.NameStrID128 AS Name,
                       c.TypeID1      AS TypeId1,
                       c.TypeID2      AS TypeId2,
                       c.TypeID3      AS TypeId3,
                       i.MaxStack     AS MaxStack,
                       CAST(i.Dur_U AS int) AS Durability,
                       c.Price        AS Price,
                       c.ReqLevel1    AS ReqLevel,
                       i.ReqStr       AS Weight
                FROM _RefObjItem i
                INNER JOIN _RefObjCommon c ON i.ID = c.ID";

            var items = await connection.QueryAsync<ItemReference>(itemSql);
            foreach (ItemReference item in items)
                _items[item.Id] = item;

            const string magicSql = @"
                SELECT ID          AS Id,
                       MOptName128  AS CodeName,
                       MOptName128  AS Name,
                       Param1       AS Param1,
                       Param2       AS Param2
                FROM _RefMagicOpt";

            var opts = await connection.QueryAsync<MagicOptReference>(magicSql);
            foreach (MagicOptReference opt in opts)
                _magicOpts[opt.Id] = opt;

            Log.Information("ItemReferenceCache yüklendi: {Items} eşya, {Opts} büyü seçeneği.",
                _items.Count, _magicOpts.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ItemReferenceCache yüklenemedi (veritabanı yok olabilir). Önbellek boş.");
        }
    }

    /// <summary>Referans kimliğine göre eşya referansı döner (yoksa null).</summary>
    public ItemReference? GetItem(int refItemId)
    {
        _items.TryGetValue(refItemId, out ItemReference? item);
        return item;
    }

    /// <summary>Kimliğe göre büyü seçeneği referansı döner (yoksa null).</summary>
    public MagicOptReference? GetMagicOpt(int id)
    {
        _magicOpts.TryGetValue(id, out MagicOptReference? opt);
        return opt;
    }

    /// <summary>
    /// Referans kimliğine göre eşya görünen adını çözer. Bulunamazsa kod adı benzeri
    /// bir yer tutucu döner.
    /// </summary>
    public string ResolveItemName(int refItemId)
    {
        ItemReference? item = GetItem(refItemId);
        if (item == null)
            return $"UNKNOWN_ITEM_{refItemId}";
        return string.IsNullOrEmpty(item.Name) ? item.CodeName : item.Name;
    }

    /// <summary>
    /// Test amaçlı: önbelleğe elle eşya referansı ekler.
    /// </summary>
    public void AddItemForTest(ItemReference item) => _items[item.Id] = item;

    /// <summary>Test amaçlı: önbelleği temizler.</summary>
    public void ClearForTest()
    {
        _items.Clear();
        _magicOpts.Clear();
    }
}
