using UnityEngine;

namespace SROClient.Models
{
    /// <summary>
    /// Dünyadaki bir varlığın (oyuncu, canavar, NPC) istemci tarafı veri modeli.
    /// Sunucudan gelen spawn/move paketleriyle doldurulur ve EntityManager tarafından
    /// sahnedeki GameObject'lerle ilişkilendirilir.
    /// </summary>
    public enum ClientEntityType : byte
    {
        Player = 0,
        Monster = 1,
        NPC = 2,
        Item = 3
    }

    /// <summary>
    /// Bir varlığın anlık durumunu tutan basit veri sınıfı.
    /// </summary>
    public sealed class EntityData
    {
        /// <summary>Sunucu tarafı benzersiz kimlik (oturum boyunca geçerli).</summary>
        public uint UniqueId { get; set; }

        /// <summary>Varlık türü.</summary>
        public ClientEntityType EntityType { get; set; }

        /// <summary>Dünya üzerindeki güncel konum.</summary>
        public Vector3 Position { get; set; }

        /// <summary>Hedef konum (interpolasyon için).</summary>
        public Vector3 TargetPosition { get; set; }

        /// <summary>Yüzey açısı (derece).</summary>
        public float Angle { get; set; }

        /// <summary>Görünen ad (karakter adı veya mob kod adı).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Seviye.</summary>
        public byte Level { get; set; }

        /// <summary>Can değeri.</summary>
        public int HP { get; set; }

        /// <summary>Maksimum can değeri.</summary>
        public int MaxHP { get; set; }
    }
}
