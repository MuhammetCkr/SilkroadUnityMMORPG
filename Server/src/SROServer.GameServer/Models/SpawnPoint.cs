namespace SROServer.GameServer.Models;

/// <summary>
/// SRO_VT_SHARD.Tab_RefNest tablosundan gelen bir canavar yuva (spawn) noktası.
/// Her yuva, belirli bir bölgede belirli bir taktik (Tab_RefTactics) ile mob üretir.
/// </summary>
public sealed class SpawnPoint
{
    /// <summary>Yuva kimliği (Tab_RefNest.dwNestID).</summary>
    public int NestID { get; set; }

    /// <summary>Yuvanın bulunduğu bölge (Tab_RefNest.nRegionDBID).</summary>
    public short RegionID { get; set; }

    // --- Yuvanın sektör içindeki yerel konumu ---
    public float LocalPosX { get; set; }
    public float LocalPosY { get; set; }
    public float LocalPosZ { get; set; }

    /// <summary>Bu yuvanın kullandığı taktik kimliği (Tab_RefTactics.dwTacticsID).</summary>
    public int TacticsID { get; set; }

    /// <summary>Bu yuvanın üreteceği mob referans kimliği (_RefObjCommon.ID).</summary>
    public int RefMonsterID { get; set; }
}
