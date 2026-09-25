using UnityEngine;
using UnityEngine.UI;
using SROClient.Gameplay;

namespace SROClient.UI.HUD
{
    /// <summary>
    /// Oyun içi baş üstü göstergesi (HUD). Yerel oyuncunun HP/MP çubuklarını, seviye ve
    /// ad bilgisini gösterir. LocalPlayer'ın değişim olaylarına abone olarak güncellenir.
    /// </summary>
    public sealed class HUDManager : MonoBehaviour
    {
        [Header("Can / Mana Çubukları")]
        [Tooltip("HP dolum çubuğu (0-1)")]
        [SerializeField] private Slider _hpSlider;

        [Tooltip("MP dolum çubuğu (0-1)")]
        [SerializeField] private Slider _mpSlider;

        [Header("Metin Alanları")]
        [Tooltip("HP sayısal metni (ör. 80/100)")]
        [SerializeField] private Text _hpText;

        [Tooltip("MP sayısal metni (ör. 50/100)")]
        [SerializeField] private Text _mpText;

        [Tooltip("Karakter adı metni")]
        [SerializeField] private Text _nameText;

        [Tooltip("Seviye metni")]
        [SerializeField] private Text _levelText;

        private LocalPlayer _player;

        private void OnEnable()
        {
            // LocalPlayer henüz hazır değilse Start'ta yeniden denenecek.
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.OnHPChanged -= UpdateHP;
                _player.OnMPChanged -= UpdateMP;
                _player.OnLevelChanged -= UpdateLevel;
            }
        }

        /// <summary>LocalPlayer olaylarına (yalnızca bir kez) abone olur.</summary>
        private void TrySubscribe()
        {
            if (_player != null || LocalPlayer.Instance == null)
                return;

            _player = LocalPlayer.Instance;
            _player.OnHPChanged += UpdateHP;
            _player.OnMPChanged += UpdateMP;
            _player.OnLevelChanged += UpdateLevel;

            // Mevcut değerlerle ilk güncellemeyi yap.
            UpdateHP(_player.HP, _player.MaxHP);
            UpdateMP(_player.MP, _player.MaxMP);
            UpdateLevel(_player.Level);

            if (_nameText != null)
                _nameText.text = _player.CharName;
        }

        private void UpdateHP(int hp, int maxHp)
        {
            if (_hpSlider != null)
                _hpSlider.value = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (_hpText != null)
                _hpText.text = $"{hp}/{maxHp}";
        }

        private void UpdateMP(int mp, int maxMp)
        {
            if (_mpSlider != null)
                _mpSlider.value = maxMp > 0 ? (float)mp / maxMp : 0f;
            if (_mpText != null)
                _mpText.text = $"{mp}/{maxMp}";
        }

        private void UpdateLevel(byte level)
        {
            if (_levelText != null)
                _levelText.text = $"Lv. {level}";
        }
    }
}
