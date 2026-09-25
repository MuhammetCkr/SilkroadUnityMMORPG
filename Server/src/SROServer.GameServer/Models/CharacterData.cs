namespace SROServer.GameServer.Models;

/// <summary>
/// SRO_VT_SHARD._User tablosundan yüklenen karakter verisi.
/// Oyuncu dünyaya girerken pozisyon ve canlılık değerleriyle birlikte kullanılır.
/// </summary>
public sealed class CharacterData
{
    /// <summary>Karakterin veritabanı kimliği (_User.CharID).</summary>
    public int CharID { get; set; }

    /// <summary>Hesap kimliği (_User.UserJID).</summary>
    public int UserJID { get; set; }

    /// <summary>Karakter adı (_User.CharName16).</summary>
    public string CharName16 { get; set; } = string.Empty;

    /// <summary>Seviye (_User.CurLevel).</summary>
    public byte CurLevel { get; set; }

    // --- Canlılık ---
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public int MP { get; set; }
    public int MaxMP { get; set; }

    // --- Pozisyon (dünya koordinatları) ---
    public float CurPosX { get; set; }
    public float CurPosY { get; set; }
    public float CurPosZ { get; set; }

    /// <summary>Bulunduğu sektör (RegionID) (_User.CurSect / LatestRegion).</summary>
    public short CurSect { get; set; }

    /// <summary>Karakterin altın (para) miktarı (_User.Gold / _Char.RemainGold).</summary>
    public long Gold { get; set; }
}
