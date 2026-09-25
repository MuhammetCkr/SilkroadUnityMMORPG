using System;
using UnityEngine;

namespace SROClient.Gameplay
{
    /// <summary>
    /// Yerel oyuncunun (bu istemciyi kontrol eden karakterin) durumunu tutan singleton.
    /// Sunucudan gelen S_INIT_DATA, S_HP_UPDATE, S_MP_UPDATE paketleriyle güncellenir.
    /// HUD gibi bileşenler değerleri okumak ve değişimleri dinlemek için buraya abone olur.
    /// </summary>
    public sealed class LocalPlayer : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static LocalPlayer Instance { get; private set; }

        /// <summary>Yerel oyuncunun sunucu tarafı benzersiz kimliği.</summary>
        public uint UniqueId { get; private set; }

        /// <summary>Karakter adı.</summary>
        public string CharName { get; private set; } = string.Empty;

        /// <summary>Seviye.</summary>
        public byte Level { get; private set; } = 1;

        // --- Canlılık değerleri ---
        public int HP { get; private set; }
        public int MaxHP { get; private set; } = 1;
        public int MP { get; private set; }
        public int MaxMP { get; private set; } = 1;

        /// <summary>Oyuncunun sahnedeki hareket kontrolcüsü (spawn edildiğinde atanır).</summary>
        public CharacterMovementController MovementController { get; set; }

        // --- Değişim olayları (HUD abone olur) ---

        /// <summary>HP değiştiğinde tetiklenir (güncel HP, maksimum HP).</summary>
        public event Action<int, int> OnHPChanged;

        /// <summary>MP değiştiğinde tetiklenir (güncel MP, maksimum MP).</summary>
        public event Action<int, int> OnMPChanged;

        /// <summary>Seviye değiştiğinde tetiklenir.</summary>
        public event Action<byte> OnLevelChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// S_INIT_DATA ile gelen başlangıç verisini uygular.
        /// </summary>
        public void Initialize(uint uniqueId, string charName, byte level,
            int hp, int maxHp, int mp, int maxMp)
        {
            UniqueId = uniqueId;
            CharName = charName;
            Level = level;
            HP = hp;
            MaxHP = Mathf.Max(1, maxHp);
            MP = mp;
            MaxMP = Mathf.Max(1, maxMp);

            // Tüm dinleyicileri başlangıç değerleriyle bilgilendir.
            OnLevelChanged?.Invoke(Level);
            OnHPChanged?.Invoke(HP, MaxHP);
            OnMPChanged?.Invoke(MP, MaxMP);
        }

        /// <summary>HP değerini günceller ve olayı tetikler.</summary>
        public void SetHP(int hp, int maxHp)
        {
            HP = hp;
            MaxHP = Mathf.Max(1, maxHp);
            OnHPChanged?.Invoke(HP, MaxHP);
        }

        /// <summary>MP değerini günceller ve olayı tetikler.</summary>
        public void SetMP(int mp, int maxMp)
        {
            MP = mp;
            MaxMP = Mathf.Max(1, maxMp);
            OnMPChanged?.Invoke(MP, MaxMP);
        }

        /// <summary>Seviyeyi günceller ve olayı tetikler.</summary>
        public void SetLevel(byte level)
        {
            Level = level;
            OnLevelChanged?.Invoke(Level);
        }
    }
}
