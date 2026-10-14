using System.Collections.Generic;
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

    // --- Envanter (Faz 3) ---

    /// <summary>Oyuncunun envanter slotları (ekipman + çanta).</summary>
    public List<InventorySlot> Inventory { get; set; } = new();

    /// <summary>Taşınan toplam ağırlık (envanterdeki eşyaların toplamı).</summary>
    public int CurrentWeight { get; set; }

    /// <summary>Taşınabilecek maksimum ağırlık (seviyeye göre belirlenir).</summary>
    public int MaxWeight { get; set; } = 10000;

    /// <summary>Oyuncunun altın (para) miktarı.</summary>
    public long Gold { get; set; }

    // --- Savaş & stat değerleri (Faz 4) ---

    /// <summary>Güç (STR) — fiziksel hasar ve HP'yi etkiler.</summary>
    public byte STR { get; set; } = 20;

    /// <summary>Zeka (INT) — büyü hasarı ve MP'yi etkiler.</summary>
    public byte INT { get; set; } = 20;

    /// <summary>Şans (Luck) — kritik vuruş şansını artırır.</summary>
    public byte Luck { get; set; }

    /// <summary>Savunma değeri (hasar azaltma için).</summary>
    public int Defense { get; set; }

    /// <summary>Mevcut deneyim puanı (EXP).</summary>
    public long CurrentExp { get; set; }

    /// <summary>Bir sonraki seviyeye geçiş için gereken toplam EXP.</summary>
    public long NextLevelExp { get; set; } = 100;

    /// <summary>Öğrenilmiş yetenek kimlikleri.</summary>
    public List<int> LearnedSkills { get; set; } = new();

    /// <summary>Yetenek bekleme süreleri: skillId -> beklemenin biteceği zaman.</summary>
    public Dictionary<int, DateTime> SkillCooldowns { get; set; } = new();

    /// <summary>Oyuncu şu anda savaşta mı?</summary>
    public bool IsInCombat { get; set; }

    /// <summary>En son saldırı yaptığı zaman (saldırı hızı throttle için).</summary>
    public DateTime LastAttackTime { get; set; } = DateTime.MinValue;

    /// <summary>Şu anki hedef varlığın benzersiz kimliği (varsa).</summary>
    public uint? TargetEntityId { get; set; }
}
