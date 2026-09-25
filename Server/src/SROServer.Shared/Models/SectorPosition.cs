namespace SROServer.Shared.Models;

/// <summary>
/// Silkroad Online'ın sektör (region) tabanlı konum sistemi.
///
/// SRO dünyası 192x192 birimlik karelere (sektör) bölünmüştür. Her sektör
/// 16-bit bir RegionID ile temsil edilir: düşük byte = X sektör indeksi,
/// yüksek byte = Y (Z) sektör indeksi. Bir varlığın konumu, ait olduğu
/// sektörün RegionID'si + o sektör içindeki yerel (local) X, Y, Z offset'i
/// ile ifade edilir.
/// </summary>
public struct SectorPosition
{
    /// <summary>Bir sektörün kenar uzunluğu (birim).</summary>
    public const float SectorSize = 192f;

    /// <summary>Sektör kimliği (RegionID).</summary>
    public short RegionId { get; set; }

    /// <summary>Sektör içindeki yerel X offset'i (0..192).</summary>
    public float LocalX { get; set; }

    /// <summary>Sektör içindeki yerel Y (yükseklik) offset'i.</summary>
    public float LocalY { get; set; }

    /// <summary>Sektör içindeki yerel Z offset'i (0..192).</summary>
    public float LocalZ { get; set; }

    public SectorPosition(short regionId, float localX, float localY, float localZ)
    {
        RegionId = regionId;
        LocalX = localX;
        LocalY = localY;
        LocalZ = localZ;
    }

    /// <summary>RegionID'nin X sektör indeksi (düşük byte).</summary>
    public byte SectorX => (byte)(RegionId & 0xFF);

    /// <summary>RegionID'nin Y sektör indeksi (yüksek byte).</summary>
    public byte SectorY => (byte)((RegionId >> 8) & 0xFF);

    /// <summary>
    /// Sektör indekslerinden RegionID üretir.
    /// </summary>
    public static short MakeRegionId(byte sectorX, byte sectorY) =>
        (short)((sectorY << 8) | sectorX);

    /// <summary>
    /// Yerel offset + RegionID'yi mutlak dünya koordinatına (SROVector3) çevirir.
    /// Dünya X = SectorX * 192 + LocalX, Dünya Z = SectorY * 192 + LocalZ.
    /// </summary>
    public SROVector3 ToWorld()
    {
        float worldX = SectorX * SectorSize + LocalX;
        float worldZ = SectorY * SectorSize + LocalZ;
        return new SROVector3(worldX, LocalY, worldZ);
    }

    /// <summary>
    /// Mutlak dünya koordinatını sektör + yerel offset'e çevirir.
    /// </summary>
    public static SectorPosition FromWorld(SROVector3 world)
    {
        byte sx = (byte)Math.Clamp((int)(world.X / SectorSize), 0, 255);
        byte sy = (byte)Math.Clamp((int)(world.Z / SectorSize), 0, 255);
        float localX = world.X - sx * SectorSize;
        float localZ = world.Z - sy * SectorSize;
        return new SectorPosition(MakeRegionId(sx, sy), localX, world.Y, localZ);
    }

    public override string ToString() =>
        $"Region={RegionId} (Sx={SectorX},Sy={SectorY}) Local=({LocalX:F1},{LocalY:F1},{LocalZ:F1})";
}
