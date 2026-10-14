using UnityEngine;
using UnityEngine.UI;

namespace SROClient.Combat
{
    /// <summary>
    /// Yetenek çubuğundaki tek bir yetenek yuvasının görünümü (Faz 4).
    /// Yetenek ikonunu, kısayol tuşunu ve bekleme süresi (cooldown) kaplamasını gösterir.
    /// Tıklanınca/kısayola basılınca bağlı yeteneği hedefe karşı kullanır.
    /// </summary>
    public sealed class SkillSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownOverlay; // radial fill (0..1)
        [SerializeField] private Text hotkeyLabel;

        /// <summary>Bu yuvaya bağlı yetenek kimliği.</summary>
        public int SkillId { get; private set; }

        private float _cooldownTotal;
        private float _cooldownRemaining;

        /// <summary>Yuvayı bir yetenek ve kısayol tuşu ile ilişkilendirir.</summary>
        public void Bind(int skillId, Sprite icon, string hotkey)
        {
            SkillId = skillId;
            if (iconImage != null) iconImage.sprite = icon;
            if (hotkeyLabel != null) hotkeyLabel.text = hotkey;
            SetCooldownVisual(0f);
        }

        /// <summary>Yetenek kullanıldığında bekleme süresini başlatır (saniye).</summary>
        public void StartCooldown(float seconds)
        {
            _cooldownTotal = Mathf.Max(0.01f, seconds);
            _cooldownRemaining = seconds;
        }

        /// <summary>Yuva tıklandığında/kısayola basıldığında çağrılır: yeteneği kullanır.</summary>
        public void Activate(uint targetId)
        {
            if (_cooldownRemaining > 0f) return; // Beklemede.
            if (CombatManager.Instance != null)
                CombatManager.Instance.RequestUseSkill(SkillId, targetId);
        }

        private void Update()
        {
            if (_cooldownRemaining <= 0f) return;

            _cooldownRemaining -= Time.deltaTime;
            if (_cooldownRemaining < 0f) _cooldownRemaining = 0f;

            SetCooldownVisual(_cooldownRemaining / _cooldownTotal);
        }

        private void SetCooldownVisual(float fill)
        {
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = fill;
                cooldownOverlay.enabled = fill > 0f;
            }
        }
    }
}
