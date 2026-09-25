using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using Serilog;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Models;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.GameServer.World;
using SROServer.Shared.Constants;
using SROServer.Shared.Models;
using SROServer.Shared.Packets;

namespace SROServer.GameServer.Network;

/// <summary>
/// LiteNetLib tabanlı oyun ağ sunucusu (varsayılan port 15001).
///
/// Bağlantı el sıkışması: istemci bağlanırken connection request verisinde
/// [connectionKey] [token] [JID] [charName] gönderir. Sunucu bunu doğrular
/// (Faz 2: stateless — token boş değil ve JID geçerli), karakteri yükler,
/// oyuncuyu dünyaya ekler ve S_INIT_DATA + çevredeki varlıkların spawn'larını yollar.
/// </summary>
public sealed class GameNetworkManager : IDisposable
{
    private const string ConnectionKey = "SROServer";

    private readonly int _port;
    private readonly int _maxConnections;
    private readonly WorldManager _world;
    private readonly SectorManager _sectorManager;
    private readonly GamePacketHandler _packetHandler;
    private readonly MovementService _movementService;
    private readonly ICharacterRepository _characterRepository;

    private readonly EventBasedNetListener _listener;
    private readonly NetManager _netManager;

    // Bağlantı (peer.Id) -> oyuncu UniqueId eşlemesi.
    private readonly ConcurrentDictionary<int, uint> _peerToPlayer = new();

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public GameNetworkManager(
        int port,
        int maxConnections,
        WorldManager world,
        SectorManager sectorManager,
        GamePacketHandler packetHandler,
        MovementService movementService,
        ICharacterRepository characterRepository)
    {
        _port = port;
        _maxConnections = maxConnections;
        _world = world;
        _sectorManager = sectorManager;
        _packetHandler = packetHandler;
        _movementService = movementService;
        _characterRepository = characterRepository;

        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener) { AutoRecycle = true };

        // Bölge oluşturuldukça spawn/despawn yayınları için olaylara abone ol.
        _world.RegionCreated += HookRegionEvents;

        RegisterEvents();
    }

    /// <summary>
    /// Bir bölgenin varlık giriş/çıkış olaylarına abone olur; böylece bir varlık
    /// bölgeye girdiğinde/çıktığında ilgili oyunculara spawn/despawn yayınlanır.
    /// </summary>
    private void HookRegionEvents(Region region)
    {
        region.EntityEntered += (r, entity) =>
        {
            // Giren varlığın spawn'ını bölgedeki diğer oyunculara bildir.
            NetPeer? exclude = (entity as PlayerEntity)?.Connection;
            byte[] spawn = _packetHandler.BuildEntitySpawnPacket(entity);
            r.BroadcastToPlayers(spawn, exclude);
        };

        region.EntityLeft += (r, entity) =>
        {
            byte[] despawn = _packetHandler.BuildEntityDespawnPacket(entity.UniqueId);
            r.BroadcastToPlayers(despawn);
        };
    }

    private void RegisterEvents()
    {
        // Bağlantı isteği: kapasite + anahtar + auth verisi kontrolü.
        _listener.ConnectionRequestEvent += OnConnectionRequest;

        _listener.PeerConnectedEvent += peer =>
            Log.Information("Oyun istemcisi bağlandı: {EndPoint}", peer);

        _listener.PeerDisconnectedEvent += OnPeerDisconnected;

        _listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) =>
        {
            byte[] data = reader.GetRemainingBytes();
            _ = ProcessPacketSafeAsync(peer, data);
            reader.Recycle();
        };
    }

    /// <summary>
    /// Bağlantı isteğini işler: anahtarı ve auth verisini doğrular, karakteri yükler,
    /// oyuncuyu dünyaya ekler ve başlangıç paketlerini gönderir.
    /// </summary>
    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_netManager.ConnectedPeersCount >= _maxConnections)
        {
            request.Reject();
            return;
        }

        // El sıkışma verisi: [key] [token] [JID] [charName].
        NetDataReader data = request.Data;
        string key = data.GetString();
        if (key != ConnectionKey)
        {
            request.Reject();
            return;
        }

        string token = data.TryGetString(out string t) ? t : string.Empty;
        int jid = data.TryGetInt(out int j) ? j : 0;
        string charName = data.TryGetString(out string c) ? c : string.Empty;

        // Faz 2 stateless doğrulama: token boş olmamalı ve JID geçerli olmalı.
        if (string.IsNullOrEmpty(token) || jid <= 0)
        {
            Log.Warning("Oyun bağlantısı reddedildi: geçersiz auth (JID={Jid}).", jid);
            request.Reject();
            return;
        }

        NetPeer peer = request.Accept();

        // Karakteri yükle ve dünyaya ekle (asenkron).
        _ = JoinWorldAsync(peer, jid, charName);
    }

    /// <summary>
    /// Karakteri veritabanından yükler (yoksa varsayılanlarla), oyuncuyu dünyaya
    /// ekler ve S_INIT_DATA + çevredeki varlıkların spawn paketlerini gönderir.
    /// </summary>
    private async Task JoinWorldAsync(NetPeer peer, int jid, string charName)
    {
        try
        {
            CharacterData? data = null;
            try
            {
                data = await _characterRepository.LoadCharacterAsync(jid, charName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Karakter yüklenemedi (veritabanı yok olabilir): {Char}", charName);
            }

            // Veritabanı yoksa geliştirme için varsayılan bir karakterle devam et.
            var player = BuildPlayerEntity(peer, jid, charName, data);

            _world.AddPlayer(player);
            _peerToPlayer[peer.Id] = player.UniqueId;

            // 1) Oyuncunun kendi başlangıç verisini gönder.
            Send(peer, _packetHandler.BuildInitDataPacket(player));

            // 2) Aynı bölgedeki mevcut diğer varlıkların spawn'larını yeni oyuncuya gönder.
            Region region = _world.GetOrCreateRegion(player.RegionId);
            foreach (EntityBase existing in region.Entities.Values)
            {
                if (existing.UniqueId == player.UniqueId) continue;
                Send(peer, _packetHandler.BuildEntitySpawnPacket(existing));
            }

            Log.Information("Oyuncu dünyaya girdi: {Char} (JID={Jid}, UID={Uid})",
                player.CharName, jid, player.UniqueId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "JoinWorld sırasında hata (JID={Jid}).", jid);
        }
    }

    /// <summary>
    /// CharacterData'dan (veya yoksa varsayılanlardan) bir PlayerEntity oluşturur.
    /// </summary>
    private PlayerEntity BuildPlayerEntity(NetPeer peer, int jid, string charName, CharacterData? data)
    {
        SROVector3 pos = data != null
            ? new SROVector3(data.CurPosX, data.CurPosY, data.CurPosZ)
            : SROVector3.Zero;

        short regionId = data?.CurSect ?? SectorManager.ResolveRegionId(pos);

        return new PlayerEntity
        {
            UniqueId = _world.GetNextEntityId(),
            JID = jid,
            CharacterId = data?.CharID ?? 0,
            CharName = data?.CharName16 ?? (string.IsNullOrEmpty(charName) ? $"Player{jid}" : charName),
            Level = data?.CurLevel ?? 1,
            HP = data?.HP ?? 100,
            MaxHP = data?.MaxHP ?? 100,
            MP = data?.MP ?? 100,
            MaxMP = data?.MaxMP ?? 100,
            Position = pos,
            RegionId = regionId,
            Connection = peer,
            State = EntityState.Idle
        };
    }

    /// <summary>
    /// Bağlantı kesildiğinde oyuncuyu dünyadan kaldırır ve pozisyonunu kaydeder.
    /// </summary>
    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Log.Information("Oyun istemcisi ayrıldı: {EndPoint}, sebep={Reason}", peer, info.Reason);

        if (_peerToPlayer.TryRemove(peer.Id, out uint uid))
        {
            PlayerEntity? player = _world.GetPlayer(uid);
            if (player != null)
            {
                // Çıkışta son pozisyonu kaydetmeye çalış (throttle'ı zorla sıfırla).
                player.LastPositionSave = DateTime.MinValue;
                _ = _movementService.ThrottleSavePositionAsync(player);
            }
            _world.RemovePlayer(uid);
        }
    }

    /// <summary>
    /// Gelen paketi ilgili oyuncuyu çözerek GamePacketHandler'a yönlendirir.
    /// </summary>
    private async Task ProcessPacketSafeAsync(NetPeer peer, byte[] data)
    {
        try
        {
            if (!_peerToPlayer.TryGetValue(peer.Id, out uint uid))
                return; // Henüz dünyaya girmemiş.

            PlayerEntity? player = _world.GetPlayer(uid);
            if (player == null) return;

            var reader = new PacketReader(data);
            switch (reader.Opcode)
            {
                case PacketOpcodes.C_MOVE_REQUEST:
                    await _packetHandler.HandleMoveRequestAsync(player, reader);
                    break;

                case PacketOpcodes.C_STOP_REQUEST:
                    _packetHandler.HandleStopRequest(player, reader);
                    break;

                case PacketOpcodes.C_SECTOR_CHANGE:
                    _packetHandler.HandleSectorChange(player, reader);
                    break;

                default:
                    Log.Warning("Bilinmeyen oyun opcode'u: 0x{Opcode:X4}", reader.Opcode);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Oyun paketi işlenirken hata.");
        }
    }

    /// <summary>Sunucuyu başlatır ve poll döngüsünü çalıştırır.</summary>
    public void Start()
    {
        if (!_netManager.Start(_port))
        {
            Log.Fatal("Oyun ağ sunucusu {Port} portunda başlatılamadı.", _port);
            throw new InvalidOperationException($"Port {_port} dinlenemiyor.");
        }

        Log.Information("Oyun ağ sunucusu {Port} portunda dinlemede.", _port);

        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
    }

    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _netManager.PollEvents();
            try
            {
                await Task.Delay(15, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Sunucuyu durdurur.</summary>
    public void Stop()
    {
        _pollCts?.Cancel();
        try { _pollTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }

        _netManager.Stop();
        Log.Information("Oyun ağ sunucusu durduruldu.");
    }

    private static void Send(NetPeer peer, byte[] data)
    {
        if (peer.ConnectionState == ConnectionState.Connected)
            peer.Send(data, DeliveryMethod.ReliableOrdered);
    }

    public void Dispose()
    {
        Stop();
        _pollCts?.Dispose();
    }
}
