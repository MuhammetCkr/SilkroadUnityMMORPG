using SROServer.Shared.Models;

namespace SROServer.GameServer.Entities;

/// <summary>
/// Bir canavarın yapay zekâ (AI) durumu.
/// </summary>
public enum MonsterState
{
    /// <summary>Yuvasında bekliyor.</summary>
    Idle,

    /// <summary>Bir oyuncuyu kovalıyor.</summary>
    Chasing,

    /// <summary>Yuvasına geri dönüyor.</summary>
    Returning,

    /// <summary>Ölü (respawn bekliyor).</summary>
    Dead
}

/// <summary>
/// Dünyadaki bir canavarı (mob) temsil eden varlık.
/// </summary>
public sealed class MonsterEntity : EntityBase
{
    public override EntityType Type => EntityType.Monster;

    /// <summary>Canavarın referans kimliği (SRO_VT_SHARD._RefObjCommon.ID).</summary>
    public int ReferenceId { get; set; }

    /// <summary>Canavarın kod adı (_RefObjCommon.CodeName128).</summary>
    public string CodeName { get; set; } = string.Empty;

    // --- Canlılık ---
    public int HP { get; set; }
    public int MaxHP { get; set; }

    /// <summary>Yapay zekâ durumu.</summary>
    public MonsterState AIState { get; set; } = MonsterState.Idle;

    /// <summary>Spawn (yuva) noktası — canavar buraya geri döner.</summary>
    public SROVector3 SpawnPoint { get; set; }

    /// <summary>Aggro (saldırı tetikleme) mesafesi.</summary>
    public float AggroRange { get; set; } = 100f;
}
