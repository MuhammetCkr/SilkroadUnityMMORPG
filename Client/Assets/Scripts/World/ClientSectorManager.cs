using UnityEngine;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.World
{
    /// <summary>
    /// İstemci tarafı sektör (bölge) takipçisi. Yerel oyuncunun konumunu izler ve
    /// SRO'nun 192 birimlik sektör sistemine göre RegionID hesaplar. Oyuncu yeni bir
    /// sektöre geçtiğinde sunucuya C_SECTOR_CHANGE bildirir ve S_SECTOR_ACK bekler.
    /// </summary>
    public sealed class ClientSectorManager : MonoBehaviour
    {
        [Header("Takip")]
        [Tooltip("İzlenecek hedef (yerel oyuncu)")]
        public Transform Target;

        [Tooltip("Bir sektörün kenar uzunluğu (SRO = 192)")]
        public float SectorSize = 192f;

        /// <summary>Oyuncunun mevcut RegionID'si.</summary>
        public short CurrentRegionId { get; private set; }

        private bool _initialized;

        private void Start()
        {
            // Sunucudan gelen sektör onayını dinle.
            NetworkManager.Instance.PacketHandler.Register(
                PacketOpcodes.S_SECTOR_ACK, HandleSectorAck);
        }

        private void Update()
        {
            if (Target == null)
                return;

            short regionId = ComputeRegionId(Target.position);

            if (!_initialized)
            {
                CurrentRegionId = regionId;
                _initialized = true;
                return;
            }

            if (regionId != CurrentRegionId)
            {
                CurrentRegionId = regionId;
                SendSectorChange(regionId);
            }
        }

        /// <summary>
        /// Dünya konumundan RegionID hesaplar (düşük byte = X sektörü, yüksek byte = Z sektörü).
        /// Sunucudaki SectorPosition.FromWorld ile birebir aynı mantık.
        /// </summary>
        public short ComputeRegionId(Vector3 worldPos)
        {
            byte sx = (byte)Mathf.Clamp(Mathf.FloorToInt(worldPos.x / SectorSize), 0, 255);
            byte sz = (byte)Mathf.Clamp(Mathf.FloorToInt(worldPos.z / SectorSize), 0, 255);
            return (short)((sz << 8) | sx);
        }

        /// <summary>Sunucuya sektör değişimini bildirir.</summary>
        private void SendSectorChange(short regionId)
        {
            NetworkManager net = NetworkManager.Instance;
            if (net == null || !net.Connection.IsConnected)
                return;

            byte[] packet = new PacketBuilder(PacketOpcodes.C_SECTOR_CHANGE)
                .WriteShort((ushort)regionId)
                .Build();

            _ = net.SendAsync(packet);
            Debug.Log($"[ClientSectorManager] Sektör değişimi bildirildi: Region={regionId}");
        }

        /// <summary>S_SECTOR_ACK: sunucunun sektör değişimini onayladığını işler.</summary>
        private void HandleSectorAck(PacketReader reader)
        {
            ushort ackRegion = reader.ReadShort();
            Debug.Log($"[ClientSectorManager] Sektör onayı alındı: Region={ackRegion}");
        }
    }
}
