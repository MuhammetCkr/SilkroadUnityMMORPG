using SROServer.Shared.Models;

namespace SROServer.GameServer.Entities;

/// <summary>
/// Bir varlığın (entity) yaşam döngüsü durumu.
/// </summary>
public enum EntityState
{
    /// <summary>Hareketsiz bekliyor.</summary>
    Idle,

    /// <summary>Hedefe doğru hareket ediyor.</summary>
    Moving,

    /// <summary>Ölü.</summary>
    Dead,

    /// <summary>Dünyaya yeni giriyor (spawn animasyonu vb.).</summary>
    Spawning
}

/// <summary>
/// Varlık türü.
/// </summary>
public enum EntityType : byte
{
    Player = 0,
    Monster = 1,
    NPC = 2,
    Item = 3
}

/// <summary>
/// Dünyadaki tüm varlıkların (oyuncu, canavar, NPC, yer eşyası) ortak temel sınıfı.
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Sunucu tarafı anlık benzersiz kimlik. WorldManager tarafından atanır.
    /// Veritabanı kimliğinden (JID/CharacterId) farklıdır; oturum boyunca geçerlidir.
    /// </summary>
    public uint UniqueId { get; set; }

    /// <summary>Dünya üzerindeki mutlak konum.</summary>
    public SROVector3 Position { get; set; }

    /// <summary>Ait olduğu SRO sektörünün RegionID'si.</summary>
    public short RegionId { get; set; }

    /// <summary>Yüzey açısı, derece (0-360). Varlığın baktığı yön.</summary>
    public float Angle { get; set; }

    /// <summary>Varlığın mevcut durumu.</summary>
    public EntityState State { get; set; } = EntityState.Idle;

    /// <summary>Varlık türü (alt sınıflar tarafından belirtilir).</summary>
    public abstract EntityType Type { get; }
}
