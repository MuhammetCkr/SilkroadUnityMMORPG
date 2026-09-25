using LiteNetLib;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Entities;

/// <summary>
/// Dünyadaki bir oyuncu karakterini temsil eden varlık.
/// Veritabanı kimliği, canlılık değerleri ve ağ bağlantısını taşır.
/// </summary>
public sealed class PlayerEntity : EntityBase
{
    public override EntityType Type => EntityType.Player;

    /// <summary>Veritabanı hesap kimliği (SRO_VT_ACCOUNT.TB_User.JID).</summary>
    public int JID { get; set; }

    /// <summary>Karakterin veritabanı kimliği (SRO_VT_SHARD._User.CharID).</summary>
    public int CharacterId { get; set; }

    /// <summary>Karakter adı.</summary>
    public string CharName { get; set; } = string.Empty;

    /// <summary>Seviye.</summary>
    public byte Level { get; set; }

    // --- Canlılık değerleri ---
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public int MP { get; set; }
    public int MaxMP { get; set; }

    /// <summary>
    /// LiteNetLib ağ bağlantısı referansı. Bu oyuncuya paket göndermek için kullanılır.
    /// </summary>
    public NetPeer? Connection { get; set; }

    /// <summary>
    /// En son pozisyon kaydının yapıldığı zaman. Pozisyon kaydını throttle etmek
    /// (her 5 saniyede bir kaydetmek) için kullanılır.
    /// </summary>
    public DateTime LastPositionSave { get; set; } = DateTime.MinValue;

    /// <summary>Oyuncu şu anda hareket halinde mi?</summary>
    public bool IsMoving { get; set; }

    /// <summary>Hareket halindeyse gidilen hedef pozisyon.</summary>
    public SROVector3 TargetPosition { get; set; }
}
