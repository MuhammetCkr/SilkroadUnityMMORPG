using LiteNetLib;
using Serilog;

namespace SROServer.Auth.Network;

/// <summary>
/// LiteNetLib tabanlı UDP ağ sunucusu.
/// İstemci bağlantılarını kabul eder ve gelen paketleri PacketHandler'a yönlendirir.
/// Varsayılan port: 15000 (appsettings.json üzerinden yapılandırılır).
/// </summary>
public sealed class AuthNetworkManager : IDisposable
{
    private const string ConnectionKey = "SROServer";

    private readonly int _port;
    private readonly int _maxConnections;
    private readonly PacketHandler _packetHandler;
    private readonly EventBasedNetListener _listener;
    private readonly NetManager _netManager;

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public AuthNetworkManager(int port, int maxConnections, PacketHandler packetHandler)
    {
        _port = port;
        _maxConnections = maxConnections;
        _packetHandler = packetHandler;

        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener)
        {
            // İstemci bağlantı isteklerini kabul etmek için gerekli.
            AutoRecycle = true
        };

        RegisterEvents();
    }

    /// <summary>
    /// LiteNetLib olaylarını (bağlantı isteği, veri alımı, kopma) kaydeder.
    /// </summary>
    private void RegisterEvents()
    {
        // Yeni bağlantı isteği: kapasite doluysa reddet, aksi halde anahtarla kabul et.
        _listener.ConnectionRequestEvent += request =>
        {
            if (_netManager.ConnectedPeersCount < _maxConnections)
                request.AcceptIfKey(ConnectionKey);
            else
                request.Reject();
        };

        _listener.PeerConnectedEvent += peer =>
            Log.Information("İstemci bağlandı: {EndPoint}", peer);

        _listener.PeerDisconnectedEvent += (peer, info) =>
            Log.Information("İstemci ayrıldı: {EndPoint}, sebep={Reason}", peer, info.Reason);

        // Veri alımı: paketi kopyalayıp PacketHandler'a asenkron ilet.
        _listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) =>
        {
            byte[] data = reader.GetRemainingBytes();
            // Fire-and-forget: işleyici içindeki hatalar yutulmadan loglanır.
            _ = ProcessPacketSafeAsync(peer, data);
            reader.Recycle();
        };
    }

    /// <summary>
    /// Paket işlemeyi hata güvenli şekilde yürütür.
    /// </summary>
    private async Task ProcessPacketSafeAsync(NetPeer peer, byte[] data)
    {
        try
        {
            await _packetHandler.HandlePacketAsync(peer, data);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Paket işlenirken hata oluştu.");
        }
    }

    /// <summary>
    /// Sunucuyu başlatır ve arka planda olay döngüsünü (polling) çalıştırır.
    /// </summary>
    public void Start()
    {
        if (!_netManager.Start(_port))
        {
            Log.Fatal("Ağ sunucusu {Port} portunda başlatılamadı.", _port);
            throw new InvalidOperationException($"Port {_port} dinlenemiyor.");
        }

        Log.Information("Auth ağ sunucusu {Port} portunda dinlemede.", _port);

        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
    }

    /// <summary>
    /// LiteNetLib olaylarını düzenli aralıklarla işleyen döngü.
    /// </summary>
    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _netManager.PollEvents();
            try
            {
                await Task.Delay(15, token); // ~66 tick/sn
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Sunucuyu durdurur ve olay döngüsünü sonlandırır.
    /// </summary>
    public void Stop()
    {
        _pollCts?.Cancel();
        try
        {
            _pollTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Döngü iptal edildiğinde beklenen durum — yok sayılır.
        }

        _netManager.Stop();
        Log.Information("Auth ağ sunucusu durduruldu.");
    }

    public void Dispose()
    {
        Stop();
        _pollCts?.Dispose();
    }
}
