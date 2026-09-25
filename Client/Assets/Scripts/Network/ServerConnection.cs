using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SROClient.Network
{
    /// <summary>
    /// Sunucuya asenkron TCP bağlantısı yöneten singleton servis (MonoBehaviour DEĞİL).
    /// Paket formatı: [Opcode (2)][Length (2)][Payload]. Length yalnızca payload boyutudur.
    /// Gelen tam paketler bir kuyruğa yazılır; NetworkManager her karede kuyruğu boşaltıp
    /// PacketHandler'a iletir (Unity ana iş parçacığı güvenliği için).
    /// </summary>
    public sealed class ServerConnection
    {
        private static readonly Lazy<ServerConnection> _instance =
            new Lazy<ServerConnection>(() => new ServerConnection());

        /// <summary>Global singleton örneği.</summary>
        public static ServerConnection Instance => _instance.Value;

        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        // Ana iş parçacığında işlenmek üzere alınan tam paketlerin kuyruğu.
        private readonly ConcurrentQueue<byte[]> _incoming = new ConcurrentQueue<byte[]>();

        /// <summary>Bağlantı kurulduğunda tetiklenir.</summary>
        public event Action? OnConnected;

        /// <summary>Bağlantı koptuğunda tetiklenir.</summary>
        public event Action? OnDisconnected;

        /// <summary>Bağlantı kurulu mu?</summary>
        public bool IsConnected => _client != null && _client.Connected;

        private ServerConnection() { }

        /// <summary>
        /// Belirtilen sunucuya asenkron bağlanır.
        /// </summary>
        public async Task ConnectAsync(string host, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();
                _cts = new CancellationTokenSource();

                // Arka planda okuma döngüsünü başlat.
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                OnConnected?.Invoke();
            }
            catch (Exception)
            {
                Disconnect();
                throw;
            }
        }

        /// <summary>
        /// Bir paketi (başlık dahil hazır byte dizisi) sunucuya asenkron gönderir.
        /// </summary>
        public async Task SendPacketAsync(byte[] packet)
        {
            if (_stream == null || !IsConnected)
                throw new InvalidOperationException("Sunucuya bağlı değil.");

            await _stream.WriteAsync(packet, 0, packet.Length);
            await _stream.FlushAsync();
        }

        /// <summary>
        /// Ağdan sürekli okuyup, [Opcode][Length] başlığına göre tam paketleri
        /// ayrıştırır ve gelen kuyruğuna ekler.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var header = new byte[4];
            try
            {
                while (!token.IsCancellationRequested && _stream != null)
                {
                    // 1) Başlığı tam olarak oku (4 byte).
                    if (!await ReadExactAsync(header, 0, 4, token))
                        break;

                    ushort length = (ushort)(header[2] | (header[3] << 8));

                    // 2) Tam paketi (başlık + payload) oluştur.
                    byte[] full = new byte[4 + length];
                    Array.Copy(header, full, 4);

                    if (length > 0)
                    {
                        if (!await ReadExactAsync(full, 4, length, token))
                            break;
                    }

                    _incoming.Enqueue(full);
                }
            }
            catch (Exception)
            {
                // Bağlantı kesildi veya okuma hatası — sessizce kapat.
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Belirtilen sayıda byte tamamen okunana kadar bekler.
        /// Bağlantı kapanırsa false döner.
        /// </summary>
        private async Task<bool> ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            int read = 0;
            while (read < count)
            {
                int n = await _stream!.ReadAsync(buffer, offset + read, count - read, token);
                if (n == 0) return false; // Bağlantı kapandı.
                read += n;
            }
            return true;
        }

        /// <summary>
        /// Ana iş parçacığından çağrılır: bekleyen tüm paketleri sırayla verilen
        /// işleyiciye iletir.
        /// </summary>
        public void ProcessIncoming(Action<byte[]> handler)
        {
            while (_incoming.TryDequeue(out byte[]? packet))
            {
                handler(packet);
            }
        }

        /// <summary>Bağlantıyı kapatır ve kaynakları serbest bırakır.</summary>
        public void Disconnect()
        {
            bool wasConnected = IsConnected;

            _cts?.Cancel();
            _stream?.Close();
            _client?.Close();
            _stream = null;
            _client = null;

            if (wasConnected)
                OnDisconnected?.Invoke();
        }
    }
}
