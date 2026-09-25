using System.Collections.Concurrent;
using Serilog;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Models;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.World;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Services;

/// <summary>
/// Canavar (mob) üretimini yöneten servis. Sunucu başlarken spawn noktalarını
/// veritabanından yükler, bölgelere mob yerleştirir ve ölen mobları zamanlayıcı
/// ile yeniden üretir (respawn).
/// </summary>
public sealed class SpawnService
{
    private readonly WorldManager _world;
    private readonly ISpawnRepository _spawnRepository;

    // RegionId -> o bölgenin spawn noktaları.
    private readonly ConcurrentDictionary<short, List<SpawnPoint>> _spawnPointsByRegion = new();

    // RefMonsterID -> mob referans şablonu (önbellek).
    private readonly ConcurrentDictionary<int, MonsterReference> _monsterRefCache = new();

    public SpawnService(WorldManager world, ISpawnRepository spawnRepository)
    {
        _world = world;
        _spawnRepository = spawnRepository;
    }

    /// <summary>Yüklenen toplam spawn noktası sayısı (tanı/istatistik için).</summary>
    public int LoadedSpawnPointCount =>
        _spawnPointsByRegion.Values.Sum(list => list.Count);

    /// <summary>
    /// Belirtilen bölge listesi için spawn noktalarını veritabanından yükler.
    /// Veritabanı erişilemezse hata loglanır ancak sunucu çalışmaya devam eder
    /// (geliştirme ortamında DB olmadan başlatmayı mümkün kılar).
    /// </summary>
    /// <param name="regionIds">Yüklenecek bölgeler. Boşsa hiçbir şey yüklenmez.</param>
    public async Task LoadSpawnPointsAsync(IEnumerable<short> regionIds)
    {
        foreach (short regionId in regionIds)
        {
            try
            {
                IEnumerable<SpawnPoint> points =
                    await _spawnRepository.GetSpawnPointsByRegionAsync(regionId);
                _spawnPointsByRegion[regionId] = points.ToList();
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "Bölge {RegionId} için spawn noktaları yüklenemedi (veritabanı yok olabilir).",
                    regionId);
                _spawnPointsByRegion[regionId] = new List<SpawnPoint>();
            }
        }

        Log.Information("Spawn noktaları yüklendi: {Count} nokta, {Regions} bölge.",
            LoadedSpawnPointCount, _spawnPointsByRegion.Count);
    }

    /// <summary>
    /// Bir bölgedeki tüm yuvalar için canavar üretir ve dünyaya ekler.
    /// </summary>
    public async Task SpawnMonstersForRegionAsync(short regionId)
    {
        if (!_spawnPointsByRegion.TryGetValue(regionId, out List<SpawnPoint>? points))
            return;

        Region region = _world.GetOrCreateRegion(regionId);

        foreach (SpawnPoint point in points)
        {
            MonsterReference? reference = await GetMonsterReferenceCachedAsync(point.RefMonsterID);
            MonsterEntity monster = CreateMonster(point, reference);
            region.OnEntityEnter(monster);
        }

        Log.Debug("Bölge {RegionId} için {Count} canavar üretildi.", regionId, points.Count);
    }

    /// <summary>
    /// Ölen bir canavarı, belirtilen gecikmeden sonra yeniden üretir (respawn).
    /// </summary>
    public Task ScheduleRespawnAsync(MonsterEntity monster, int delaySeconds)
    {
        // Fire-and-forget respawn zamanlayıcısı.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

            // Canavarı yuvasına, tam canla geri getir.
            monster.Position = monster.SpawnPoint;
            monster.HP = monster.MaxHP;
            monster.AIState = MonsterState.Idle;
            monster.State = EntityState.Idle;

            Region region = _world.GetOrCreateRegion(monster.RegionId);
            region.OnEntityEnter(monster);

            Log.Debug("Canavar respawn oldu: {Code} (UID={Uid})",
                monster.CodeName, monster.UniqueId);
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Bir spawn noktası + referanstan yeni bir MonsterEntity oluşturur.
    /// </summary>
    private MonsterEntity CreateMonster(SpawnPoint point, MonsterReference? reference)
    {
        // Yerel offset + region'dan dünya pozisyonu hesapla.
        var sector = new SectorPosition(
            point.RegionID, point.LocalPosX, point.LocalPosY, point.LocalPosZ);
        SROVector3 worldPos = sector.ToWorld();

        int maxHp = reference?.HP ?? 100;

        return new MonsterEntity
        {
            UniqueId = _world.GetNextEntityId(),
            ReferenceId = point.RefMonsterID,
            CodeName = reference?.CodeName ?? $"MOB_{point.RefMonsterID}",
            HP = maxHp,
            MaxHP = maxHp,
            Position = worldPos,
            SpawnPoint = worldPos,
            RegionId = point.RegionID,
            AIState = MonsterState.Idle,
            State = EntityState.Idle
        };
    }

    /// <summary>
    /// Mob referansını önbellekten getirir; yoksa veritabanından çeker.
    /// </summary>
    private async Task<MonsterReference?> GetMonsterReferenceCachedAsync(int refId)
    {
        if (_monsterRefCache.TryGetValue(refId, out MonsterReference? cached))
            return cached;

        try
        {
            MonsterReference? reference = await _spawnRepository.GetMonsterReferenceAsync(refId);
            if (reference != null)
                _monsterRefCache[refId] = reference;
            return reference;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Mob referansı yüklenemedi: RefId={RefId}", refId);
            return null;
        }
    }
}
