using System.Collections.Concurrent;
using LiteNetLib;
using SROServer.GameServer.Entities;

namespace SROServer.GameServer.World;

/// <summary>
/// Silkroad'ın 192x192 birimlik bölge (sektör) sistemindeki tek bir bölgeyi temsil eder.
/// Bir bölgedeki tüm varlıkları tutar ve o bölgedeki oyunculara paket yayınlar (broadcast).
///
/// Paket üretimi (spawn/despawn içeriği) ağ katmanında yapılır; Region yalnızca
/// varlık giriş/çıkışını olay (event) olarak duyurur. Bu, döngüsel bağımlılığı önler
/// (Network -> World -> Network olmasın diye).
/// </summary>
public sealed class Region
{
    /// <summary>Bu bölgenin RegionID'si.</summary>
    public short RegionId { get; }

    /// <summary>Bölgedeki tüm varlıklar (UniqueId -> Entity).</summary>
    public ConcurrentDictionary<uint, EntityBase> Entities { get; } = new();

    /// <summary>Bir varlık bölgeye girdiğinde tetiklenir (bölge, giren varlık).</summary>
    public event Action<Region, EntityBase>? EntityEntered;

    /// <summary>Bir varlık bölgeden ayrıldığında tetiklenir (bölge, ayrılan varlık).</summary>
    public event Action<Region, EntityBase>? EntityLeft;

    public Region(short regionId)
    {
        RegionId = regionId;
    }

    /// <summary>
    /// Bir varlığı bölgeye ekler ve giriş olayını duyurur.
    /// </summary>
    public void OnEntityEnter(EntityBase entity)
    {
        Entities[entity.UniqueId] = entity;
        entity.RegionId = RegionId;
        EntityEntered?.Invoke(this, entity);
    }

    /// <summary>
    /// Bir varlığı bölgeden çıkarır ve ayrılma olayını duyurur.
    /// </summary>
    public void OnEntityLeave(EntityBase entity)
    {
        if (Entities.TryRemove(entity.UniqueId, out _))
        {
            EntityLeft?.Invoke(this, entity);
        }
    }

    /// <summary>
    /// Bölgedeki tüm oyunculara bir paket gönderir.
    /// </summary>
    /// <param name="packet">Gönderilecek hazır paket (başlık dahil).</param>
    /// <param name="exclude">Hariç tutulacak bağlantı (ör. paketi tetikleyen oyuncu).</param>
    public void BroadcastToPlayers(byte[] packet, NetPeer? exclude = null)
    {
        foreach (EntityBase entity in Entities.Values)
        {
            if (entity is PlayerEntity player &&
                player.Connection != null &&
                player.Connection.ConnectionState == ConnectionState.Connected &&
                !ReferenceEquals(player.Connection, exclude))
            {
                player.Connection.Send(packet, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    /// <summary>Bölgedeki oyuncu sayısı.</summary>
    public int PlayerCount
    {
        get
        {
            int count = 0;
            foreach (EntityBase e in Entities.Values)
                if (e is PlayerEntity) count++;
            return count;
        }
    }
}
