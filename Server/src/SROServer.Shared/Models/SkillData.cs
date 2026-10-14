namespace SROServer.Shared.Models;

/// <summary>
/// Bir yeteneğin (skill) statik referans verisi (_RefSkill tabanlı).
/// Sunucu başlangıcında bellekte önbelleğe alınır ve tüm oyuncular için ortaktır.
/// Bu sınıf istemci ve sunucu arasında paylaşılır.
/// </summary>
public sealed class SkillData
{
    /// <summary>Yetenek referans kimliği (_RefSkill.ID).</summary>
    public int Id { get; set; }

    /// <summary>Kod adı (_RefSkill.Basic_Code / CodeName128).</summary>
    public string CodeName { get; set; } = string.Empty;

    /// <summary>Görünen ad (istemcide gösterilecek okunabilir isim).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ait olduğu ustalık (mastery) kimliği (_RefSkillMastery.ID).</summary>
    public int MasteryId { get; set; }

    /// <summary>Kullanmak için gereken seviye (_RefSkill.ReqLevel1).</summary>
    public byte ReqLevel { get; set; }

    /// <summary>Kullanım başına harcanan MP (_RefSkill.MP_Consume).</summary>
    public int MPCost { get; set; }

    /// <summary>Bekleme süresi, milisaniye (_RefSkill.CoolTime).</summary>
    public int Cooldown { get; set; }

    /// <summary>Etki menzili (dünya birimi).</summary>
    public float Range { get; set; }

    /// <summary>Temel hasar/çarpan parametresi (_RefSkill.Param1).</summary>
    public int Damage { get; set; }

    /// <summary>Hedef türü: 0=Kendisi, 1=Düşman, 2=Grup, 3=Alan (AoE).</summary>
    public byte TargetType { get; set; }
}
