using Serilog;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.World;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Services;

/// <summary>
/// Bir hareket isteğinin işlenme sonucu.
/// </summary>
public sealed class MoveResult
{
    /// <summary>Hareket kabul edildi mi?</summary>
    public bool Accepted { get; init; }

    /// <summary>Onaylanan (gerekirse düzeltilmiş) hedef pozisyon.</summary>
    public SROVector3 TargetPosition { get; init; }

    /// <summary>Bu hareket bir bölge değişimine yol açtı mı?</summary>
    public bool RegionChanged { get; init; }

    /// <summary>Reddedildiyse sebep (log/tanı için).</summary>
    public string? RejectReason { get; init; }

    public static MoveResult Reject(string reason) =>
        new() { Accepted = false, RejectReason = reason };

    public static MoveResult Ok(SROVector3 target, bool regionChanged) =>
        new() { Accepted = true, TargetPosition = target, RegionChanged = regionChanged };
}

/// <summary>
/// Oyuncu hareketini işleyen servis: pozisyon doğrulama (anti-cheat temeli),
/// bölge geçişi kontrolü ve throttle'lı pozisyon kaydı.
/// </summary>
public sealed class MovementService
{
    // Bir tick'te (istek başına) izin verilen maksimum sıçrama mesafesi (ışınlanma/hile tespiti).
    // Oyuncu hızı ~7 birim/sn olduğundan, tek istekte 250 birimden fazla atlama şüphelidir.
    private const float MaxMoveDistance = 250f;

    // Pozisyon kaydı throttle aralığı.
    private static readonly TimeSpan PositionSaveInterval = TimeSpan.FromSeconds(5);

    private readonly WorldManager _world;
    private readonly SectorManager _sectorManager;
    private readonly ICharacterRepository _characterRepository;

    public MovementService(
        WorldManager world,
        SectorManager sectorManager,
        ICharacterRepository characterRepository)
    {
        _world = world;
        _sectorManager = sectorManager;
        _characterRepository = characterRepository;
    }

    /// <summary>
    /// Bir oyuncunun hareket isteğini işler. Mesafeyi doğrular, oyuncu durumunu
    /// günceller ve bölge değişimi gerekiyorsa gerçekleştirir.
    /// </summary>
    public Task<MoveResult> ProcessMoveRequestAsync(PlayerEntity player, SROVector3 targetPos)
    {
        // 1) Mesafe doğrulaması — aşırı sıçramayı reddet.
        float distance = player.Position.DistanceTo(targetPos);
        if (distance > MaxMoveDistance)
        {
            Log.Warning("Şüpheli hareket reddedildi: {Char} mesafe={Dist:F1}",
                player.CharName, distance);
            return Task.FromResult(MoveResult.Reject($"Mesafe çok büyük ({distance:F1})."));
        }

        // 2) Bölge değişimi gerekiyor mu?
        bool regionChanged = NeedsRegionChange(player.Position, targetPos, player.RegionId);

        // 3) Oyuncu hareket durumunu güncelle.
        player.IsMoving = true;
        player.TargetPosition = targetPos;
        player.State = EntityState.Moving;

        // Sunucu, oyuncunun konumunu hedefe ilerlemiş kabul eder (istemci interpolasyonu ana
        // görsel akışı yürütür; sunucu otoritesi hedef pozisyondur).
        player.Position = targetPos;

        if (regionChanged)
        {
            _sectorManager.HandlePlayerMovement(player, targetPos);
        }

        return Task.FromResult(MoveResult.Ok(targetPos, regionChanged));
    }

    /// <summary>
    /// Bir oyuncunun pozisyonunu, en son kayıttan bu yana 5 saniye geçtiyse kaydeder.
    /// </summary>
    public async Task ThrottleSavePositionAsync(PlayerEntity player)
    {
        DateTime now = DateTime.UtcNow;
        if (now - player.LastPositionSave < PositionSaveInterval)
            return; // Henüz zamanı gelmedi.

        player.LastPositionSave = now;
        try
        {
            await _characterRepository.SavePositionAsync(player.JID, player.Position, player.RegionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Pozisyon kaydedilemedi: {Char}", player.CharName);
        }
    }

    /// <summary>
    /// Hedef pozisyonun mevcut bölgeden farklı bir sektöre denk gelip gelmediğini kontrol eder.
    /// </summary>
    public bool NeedsRegionChange(SROVector3 current, SROVector3 target, short currentRegion)
    {
        short targetRegion = SectorManager.ResolveRegionId(target);
        return targetRegion != currentRegion;
    }
}
