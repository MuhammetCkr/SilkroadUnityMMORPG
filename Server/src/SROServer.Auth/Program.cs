using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using SROServer.Auth.Network;
using SROServer.Auth.Repositories;
using SROServer.Auth.Services;

// ------------------------------------------------------------------
// SROServer.Auth — Kimlik Doğrulama Sunucusu (Faz 1)
// Kullanıcı girişini doğrular, oturum tokenı üretir ve karakter listesini sunar.
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
Log.Information(" SROServer.Auth başlatılıyor (Faz 1)");
Log.Information("====================================");

try
{
    // 3) Yapılandırma değerlerini al.
    int authPort = int.TryParse(config["Server:AuthPort"], out int p) ? p : 15000;
    int maxConn = int.TryParse(config["Server:MaxConnections"], out int m) ? m : 1000;
    string accountCs = config["Database:AccountConnectionString"] ?? string.Empty;
    string shardCs = config["Database:ShardConnectionString"] ?? string.Empty;

    // 4) Bağımlılıkları oluştur (elle DI — küçük servis için yeterli).
    var userRepository = new UserRepository(accountCs, shardCs);
    var tokenService = new TokenService();
    var authService = new AuthService(userRepository, tokenService);
    var packetHandler = new PacketHandler(authService, tokenService);

    // 5) Ağ sunucusunu başlat.
    using var networkManager = new AuthNetworkManager(authPort, maxConn, packetHandler);
    networkManager.Start();

    // 6) Graceful shutdown — CTRL+C ile temiz kapanış.
    var shutdownEvent = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true; // Süreci hemen sonlandırma, temiz kapanışa izin ver.
        Log.Information("Kapatma sinyali alındı, sunucu durduruluyor...");
        shutdownEvent.Set();
    };

    Log.Information("Sunucu hazır. Durdurmak için CTRL+C.");
    shutdownEvent.Wait();

    networkManager.Stop();
    Log.Information("SROServer.Auth temiz şekilde kapatıldı.");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "SROServer.Auth kritik bir hata ile sonlandı.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
