using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using SROServer.GameServer.Network;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.GameServer.World;

// ------------------------------------------------------------------
// SROServer.GameServer — Oyun Sunucusu (Faz 2)
// Dünya durumu, varlık senkronizasyonu ve hareket sistemini yönetir.
//   - LiteNetLib tabanlı UDP oyun ağ sunucusu (varsayılan port 15001)
//   - Bölge (sektör) bazlı dünya yönetimi ve ilgi (interest) yönetimi
//   - Otoriter hareket doğrulama + throttle'lı pozisyon kaydı
//   - Sabit adımlı oyun döngüsü (varsayılan 100ms tick)
// ------------------------------------------------------------------

// 1) Yapılandırmayı oku (appsettings.json).
IConfigurationRoot config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// 2) Serilog logger'ı kur (konsola yazar).
string minLevelStr = config["Serilog:MinimumLevel"] ?? "Information";
LogEventLevel minLevel = Enum.TryParse(minLevelStr, out LogEventLevel parsed)
    ? parsed
    : LogEventLevel.Information;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(minLevel)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Information("====================================");
Log.Information(" SROServer.GameServer başlatılıyor (Faz 2)");
Log.Information("====================================");

try
{
    // 3) Yapılandırma değerlerini al.
    int gamePort = int.TryParse(config["Server:GamePort"], out int gp) ? gp : 15001;
    int maxConn = int.TryParse(config["Server:MaxConnections"], out int mc) ? mc : 1000;
    int tickRateMs = int.TryParse(config["Server:TickRateMs"], out int tr) ? tr : 100;
    string shardCs = config["Database:ShardConnectionString"] ?? string.Empty;

    // Önceden yüklenecek bölgeler (opsiyonel; mob spawn için).
    // Binder paketine bağımlılık eklememek için değerler elle ayrıştırılır.
    short[] preloadRegions = config.GetSection("World:PreloadRegions")
        .GetChildren()
        .Select(c => short.TryParse(c.Value, out short r) ? r : (short?)null)
        .Where(r => r.HasValue)
        .Select(r => r!.Value)
        .ToArray();

    // 4) Bağımlılıkları oluştur (elle DI — küçük servis için yeterli).
    WorldManager world = WorldManager.Instance;
    var sectorManager = new SectorManager(world);

    var characterRepository = new CharacterRepository(shardCs);
    var spawnRepository = new SpawnRepository(shardCs);

    var movementService = new MovementService(world, sectorManager, characterRepository);
    var spawnService = new SpawnService(world, spawnRepository);
    var packetHandler = new GamePacketHandler(world, movementService);

    // 5) Spawn noktalarını yükle ve önceden tanımlı bölgelerde mob üret
    //    (veritabanı yoksa uyarı loglanır, sunucu yine de başlar).
    if (preloadRegions.Length > 0)
    {
        await spawnService.LoadSpawnPointsAsync(preloadRegions);
        foreach (short regionId in preloadRegions)
            await spawnService.SpawnMonstersForRegionAsync(regionId);
    }

    // 6) Ağ sunucusunu başlat.
    using var networkManager = new GameNetworkManager(
        gamePort, maxConn, world, sectorManager, packetHandler, movementService, characterRepository);
    networkManager.Start();

    // 7) Sabit adımlı oyun döngüsü (tick). WorldManager.Update her tick'te çağrılır.
    float deltaSeconds = tickRateMs / 1000f;
    using var gameLoopTimer = new Timer(
        _ =>
        {
            try
            {
                world.Update(deltaSeconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Oyun döngüsü tick'inde hata.");
            }
        },
        state: null,
        dueTime: tickRateMs,
        period: tickRateMs);

    Log.Information("Oyun döngüsü başlatıldı: {TickMs}ms tick ({Tps:F1} tick/sn).",
        tickRateMs, 1000f / tickRateMs);

    // 8) Graceful shutdown — CTRL+C ile temiz kapanış.
    var shutdownEvent = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true; // Süreci hemen sonlandırma, temiz kapanışa izin ver.
        Log.Information("Kapatma sinyali alındı, sunucu durduruluyor...");
        shutdownEvent.Set();
    };

    Log.Information("Sunucu hazır (port {Port}). Durdurmak için CTRL+C.", gamePort);
    shutdownEvent.Wait();

    networkManager.Stop();
    Log.Information("SROServer.GameServer temiz şekilde kapatıldı.");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "SROServer.GameServer kritik bir hata ile sonlandı.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
