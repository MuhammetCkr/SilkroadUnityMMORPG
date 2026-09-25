using System;
using System.Threading.Tasks;
using SROClient.Network;
using UnityEngine;

namespace SROClient.Managers
{
    /// <summary>
    /// Ağ katmanını Unity yaşam döngüsüne bağlayan singleton MonoBehaviour.
    /// ServerConnection örneğini tutar, PacketHandler'ı yönetir ve gelen paketleri
    /// her karede (Update) ana iş parçacığında işler.
    /// </summary>
    public sealed class NetworkManager : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static NetworkManager Instance { get; private set; }

        [Header("Sunucu Ayarları")]
        [Tooltip("Auth sunucusu adresi")]
        public string Host = "127.0.0.1";

        [Tooltip("Auth sunucusu portu")]
        public int Port = 15000;

        /// <summary>Paket dağıtıcısı — UI ve yöneticiler buraya işleyici kaydeder.</summary>
        public PacketHandler PacketHandler { get; private set; }

        /// <summary>Aktif sunucu bağlantısı.</summary>
        public ServerConnection Connection => ServerConnection.Instance;

        private void Awake()
        {
            // Singleton kurulumu — sahne geçişlerinde korunur.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            PacketHandler = new PacketHandler();
        }

        /// <summary>Sunucuya asenkron bağlanır.</summary>
        public async Task ConnectAsync()
        {
            await Connection.ConnectAsync(Host, Port);
            Debug.Log($"[NetworkManager] Sunucuya bağlanıldı: {Host}:{Port}");
        }

        /// <summary>Hazır bir paketi sunucuya gönderir.</summary>
        public async Task SendAsync(byte[] packet)
        {
            await Connection.SendPacketAsync(packet);
        }

        private void Update()
        {
            // Arka planda gelen paketleri ana iş parçacığında işle (Unity API güvenliği).
            Connection.ProcessIncoming(data => PacketHandler.Dispatch(data));
        }

        private void OnApplicationQuit()
        {
            Connection.Disconnect();
        }
    }
}
