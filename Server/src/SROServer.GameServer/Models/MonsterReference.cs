namespace SROServer.GameServer.Models;

/// <summary>
/// SRO_VT_SHARD._RefObjCommon tablosundan gelen bir canavar (mob) referans/şablon verisi.
/// Aynı türden tüm canavarlar bu şablonu paylaşır.
/// </summary>
public sealed class MonsterReference
{
    /// <summary>Referans kimliği (_RefObjCommon.ID).</summary>
    public int ID { get; set; }

    /// <summary>Kod adı (_RefObjCommon.CodeName128).</summary>
    public string CodeName { get; set; } = string.Empty;

    /// <summary>İsim string kimliği (_RefObjCommon.NameStrID128).</summary>
    public string NameStr { get; set; } = string.Empty;

    /// <summary>Maksimum can puanı (_RefObjCommon.MaxHP / ilgili alan).</summary>
    public int HP { get; set; }

    /// <summary>Seviye (varsa).</summary>
    public byte Level { get; set; }
}
