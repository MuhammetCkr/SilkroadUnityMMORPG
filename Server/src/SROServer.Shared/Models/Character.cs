namespace SROServer.Shared.Models;

/// <summary>
/// Oyun karakteri modeli. SRO_VT_SHARD veritabanındaki _User tablosundan map edilir.
/// Bu model hem sunucuda hem de (kopyası) istemcide karakter listesi için kullanılır.
/// </summary>
public sealed class Character
{
    /// <summary>Karakter adı (_User.CharName16).</summary>
    public string CharName16 { get; set; } = string.Empty;

    /// <summary>Mevcut seviye (_User.CurLevel).</summary>
    public byte CurLevel { get; set; }

    /// <summary>Ulaşılan en yüksek seviye (_User.MaxLevel).</summary>
    public byte MaxLevel { get; set; }

    /// <summary>Mevcut can puanı (_User.HP).</summary>
    public int HP { get; set; }

    /// <summary>Mevcut mana puanı (_User.MP).</summary>
    public int MP { get; set; }

    /// <summary>Dağıtılmamış stat puanı (_User.RemainStatPoint).</summary>
    public int RemainStatPoint { get; set; }

    /// <summary>PVP durumu (_User.PVPState).</summary>
    public byte PVPState { get; set; }

    /// <summary>Dünya üzerindeki X koordinatı.</summary>
    public float PositionX { get; set; }

    /// <summary>Dünya üzerindeki Y koordinatı (yükseklik).</summary>
    public float PositionY { get; set; }

    /// <summary>Dünya üzerindeki Z koordinatı.</summary>
    public float PositionZ { get; set; }
}
