using System.Collections.Concurrent;
using Serilog;
using SROServer.GameServer.Entities;

namespace SROServer.GameServer.World;

/// <summary>
/// Dünyanın merkezi yöneticisi (singleton). Tüm aktif bölgeleri, oyuncuları ve
/// benzersiz varlık kimliği üretimini yönetir. Oyun döngüsü (tick) buradan sürülür.
/// </summary>
public sealed class WorldManager
{
    private static readonly Lazy<WorldManager> _instance = new(() => new WorldManager());

    /// <summary>Global singleton örneği.</summary>
    public static WorldManager Instance => _instance.Value;

    // RegionId -> Region eşlemesi.
    private readonly ConcurrentDictionary<short, Region> _regions = new();

    // Aktif oyuncular: UniqueId -> PlayerEntity.
    private readonly ConcurrentDictionary<uint, PlayerEntity> _players = new();

    // JID -> UniqueId hızlı arama (aynı hesabın tekrar girişini yakalamak için).
    private readonly ConcurrentDictionary<int, uint> _jidToUniqueId = new();

    // Benzersiz varlık kimliği sayacı (1'den başlar).
    private uint _entityIdCounter = 0;

    /// <summary>Bir bölge oluşturulduğunda tetiklenir (ör. ağ katmanı event abonesi olması için).</summary>
    public event Action<Region>? RegionCreated;

    private WorldManager() { }

    /// <summary>
    /// Sıradaki benzersiz varlık kimliğini (thread-safe) üretir.
    /// </summary>
    public uint GetNextEntityId() => Interlocked.Increment(ref _entityIdCounter);

    /// <summary>
    /// Belirtilen RegionID için bölgeyi getirir; yoksa oluşturur.
    /// </summary>
    public Region GetOrCreateRegion(short regionId)
    {
        return _regions.GetOrAdd(regionId, id =>
        {
            var region = new Region(id);
            Log.Debug("Yeni bölge oluşturuldu: RegionId={RegionId}", id);
            RegionCreated?.Invoke(region);
            return region;
        });
    }

    /// <summary>Var olan bir bölgeyi getirir; yoksa null.</summary>
    public Region? GetRegion(short regionId) =>
        _regions.TryGetValue(regionId, out var region) ? region : null;

    /// <summary>Tüm aktif bölgeler.</summary>
    public IEnumerable<Region> AllRegions => _regions.Values;

    /// <summary>
    /// Bir oyuncuyu dünyaya ekler: uygun bölgeye yerleştirir ve indekslere kaydeder.
    /// </summary>
    public void AddPlayer(PlayerEntity player)
    {
        _players[player.UniqueId] = player;
        _jidToUniqueId[player.JID] = player.UniqueId;

        Region region = GetOrCreateRegion(player.RegionId);
        region.OnEntityEnter(player);

        Log.Information("Oyuncu dünyaya eklendi: {Char} (UID={Uid}, Region={Region})",
            player.CharName, player.UniqueId, player.RegionId);
    }

    /// <summary>
    /// Bir oyuncuyu dünyadan kaldırır: bulunduğu bölgeden çıkarır ve indekslerden siler.
    /// </summary>
    public void RemovePlayer(uint uniqueId)
    {
        if (_players.TryRemove(uniqueId, out PlayerEntity? player))
        {
            _jidToUniqueId.TryRemove(player.JID, out _);
            Region? region = GetRegion(player.RegionId);
            region?.OnEntityLeave(player);

            Log.Information("Oyuncu dünyadan kaldırıldı: {Char} (UID={Uid})",
                player.CharName, uniqueId);
        }
    }

    /// <summary>UniqueId ile oyuncu getirir; yoksa null.</summary>
    public PlayerEntity? GetPlayer(uint uniqueId) =>
        _players.TryGetValue(uniqueId, out var p) ? p : null;

    /// <summary>JID ile oyuncu getirir; yoksa null.</summary>
    public PlayerEntity? GetPlayerByJID(int jid) =>
        _jidToUniqueId.TryGetValue(jid, out uint uid) ? GetPlayer(uid) : null;

    /// <summary>Aktif oyuncu sayısı.</summary>
    public int PlayerCount => _players.Count;

    /// <summary>Tüm aktif oyuncular.</summary>
    public IEnumerable<PlayerEntity> AllPlayers => _players.Values;

    /// <summary>
    /// Oyun döngüsünün her tick'inde (varsayılan 100ms) çağrılır.
    /// Şimdilik: hareket eden oyuncuların ilerlemesini ve basit mob AI'ını günceller.
    /// Faz 2+'da savaş ve gelişmiş AI buraya eklenecektir.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        // Faz 2: her tick bölge bazlı güncelleme yer tutucusu.
        // Hareket interpolasyonu istemci tarafında yapıldığı için sunucu yalnızca
        // hedef pozisyonları ve durum değişimlerini yayınlar (event-driven).
        // Gelecekte mob AI adımları (Chasing/Returning) burada işlenecektir.
    }
}
