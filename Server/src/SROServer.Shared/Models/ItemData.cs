namespace SROServer.Shared.Models;

/// <summary>
/// Bir oyuncu envanterindeki somut bir eşya örneğini temsil eder.
/// Hem veritabanı örnek verisi (_Items) hem de referans (statik) verisi
/// (_RefObjCommon / _RefObjItem) birleştirilerek doldurulur.
/// Bu sınıf istemci ve sunucu arasında paylaşılır.
/// </summary>
public sealed class ItemData
{
    /// <summary>Eşyanın benzersiz veritabanı kimliği (_Items.ID64).</summary>
    public long Id64 { get; set; }

    /// <summary>Referans eşya kimliği (_Items.RefItemID -> _RefObjCommon.ID).</summary>
    public int RefItemId { get; set; }

    /// <summary>Referans kod adı (_RefObjCommon.CodeName128), örn. ITEM_ETC_HP_POTION_01.</summary>
    public string CodeName { get; set; } = string.Empty;

    /// <summary>Görünen ad (istemcide gösterilecek okunabilir isim).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Yükseltme (plus) seviyesi (_Items.OptLevel).</summary>
    public byte OptLevel { get; set; }

    /// <summary>Büyü parametresi sayısı (_Items.MagParamNum).</summary>
    public byte MagParamNum { get; set; }

    /// <summary>Büyü parametreleri (_Items.MagParam1..12). Her biri 8 byte'lık paketlenmiş değer.</summary>
    public long[] MagParams { get; set; } = System.Array.Empty<long>();

    // --- Referans (statik) veriden gelen tip bilgisi ---
    public int TypeId1 { get; set; }
    public int TypeId2 { get; set; }
    public int TypeId3 { get; set; }

    /// <summary>Bir slotta üst üste yığılabilecek maksimum adet (_RefObjItem.MaxStack).</summary>
    public int MaxStack { get; set; }

    /// <summary>Dayanıklılık (_RefObjItem.Dur_U tabanlı).</summary>
    public int Durability { get; set; }

    /// <summary>Temel fiyat (_RefObjCommon.Price).</summary>
    public int Price { get; set; }

    /// <summary>Kullanım için gereken seviye (_RefObjCommon.ReqLevel1).</summary>
    public byte RequiredLevel { get; set; }

    /// <summary>Ağırlık (envanter yük hesabı için).</summary>
    public int Weight { get; set; }

    /// <summary>Bu örnekteki adet (yığılabilir eşyalar için).</summary>
    public int Quantity { get; set; } = 1;
}
