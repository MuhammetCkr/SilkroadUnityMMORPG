namespace SROServer.Shared.Models;

/// <summary>
/// Tek bir saldırının (temel veya yetenekli) sonucunu temsil eder.
/// Sunucu hasar hesaplaması sonrası üretilir ve S_ATTACK_RESULT paketiyle istemciye
/// bildirilir. İstemci ve sunucu arasında paylaşılır.
/// </summary>
public sealed class CombatResult
{
    /// <summary>Saldıran varlığın anlık benzersiz kimliği.</summary>
    public uint AttackerId { get; set; }

    /// <summary>Hedef varlığın anlık benzersiz kimliği.</summary>
    public uint TargetId { get; set; }

    /// <summary>Verilen nihai hasar.</summary>
    public int Damage { get; set; }

    /// <summary>Saldırı kritik vuruş mu?</summary>
    public bool IsCritical { get; set; }

    /// <summary>Hedef bu saldırıyla öldü mü?</summary>
    public bool IsTargetDead { get; set; }
}
