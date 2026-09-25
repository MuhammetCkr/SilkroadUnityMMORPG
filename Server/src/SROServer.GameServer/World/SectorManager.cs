using Serilog;
using SROServer.GameServer.Entities;
using SROServer.Shared.Models;

namespace SROServer.GameServer.World;

/// <summary>
/// Bölgeler (sektörler) arası geçişi yönetir. Bir oyuncu bir sektörden diğerine
/// geçtiğinde eski bölgeden çıkarır, yeni bölgeye ekler ve ilgili bölgeleri günceller.
/// </summary>
public sealed class SectorManager
{
    private readonly WorldManager _world;

    public SectorManager(WorldManager world)
    {
        _world = world;
    }

    /// <summary>
    /// Verilen dünya konumundan hedef RegionID'yi hesaplar.
    /// </summary>
    public static short ResolveRegionId(SROVector3 worldPosition)
    {
        return SectorPosition.FromWorld(worldPosition).RegionId;
    }

    /// <summary>
    /// Bir oyuncunun hedef pozisyonuna göre bölge değişimi gerekip gerekmediğini
    /// kontrol eder; gerekiyorsa oyuncuyu yeni bölgeye taşır.
    /// </summary>
    /// <returns>Bölge değişimi yapıldıysa true.</returns>
    public bool HandlePlayerMovement(PlayerEntity player, SROVector3 targetPosition)
    {
        short newRegionId = ResolveRegionId(targetPosition);
        if (newRegionId == player.RegionId)
            return false; // Aynı bölge — değişim yok.

        TransferEntity(player, player.RegionId, newRegionId);
        return true;
    }

    /// <summary>
    /// Bir varlığı eski bölgeden çıkarıp yeni bölgeye ekler.
    /// </summary>
    public void TransferEntity(EntityBase entity, short fromRegionId, short toRegionId)
    {
        Region? oldRegion = _world.GetRegion(fromRegionId);
        oldRegion?.OnEntityLeave(entity);

        Region newRegion = _world.GetOrCreateRegion(toRegionId);
        newRegion.OnEntityEnter(entity);

        Log.Debug("Varlık bölge değiştirdi: UID={Uid}, {From} -> {To}",
            entity.UniqueId, fromRegionId, toRegionId);
    }
}
