using UnityEngine;
using SROClient.Gameplay;

namespace SROClient.Combat
{
    /// <summary>
    /// Ekranın altındaki yetenek çubuğu (Faz 4). Birden çok SkillSlotUI tutar,
    /// kısayol tuşlarını (1-8) dinler ve seçili hedefe karşı yetenekleri kullanır.
    /// Ayrıca CombatManager.OnSkillResult olayına abone olarak bekleme sürelerini başlatır.
    /// </summary>
    public sealed class SkillBar : MonoBehaviour
    {
        [SerializeField] private SkillSlotUI[] slots = new SkillSlotUI[8];

        // Kısayol tuşları (yuva sırasına karşılık gelir).
        private static readonly KeyCode[] Hotkeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8
        };

        private void Start()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnSkillResult += HandleSkillResult;
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
                CombatManager.Instance.OnSkillResult -= HandleSkillResult;
        }

        private void Update()
        {
            for (int i = 0; i < slots.Length && i < Hotkeys.Length; i++)
            {
                if (slots[i] != null && Input.GetKeyDown(Hotkeys[i]))
                    slots[i].Activate(GetCurrentTargetId());
            }
        }

        /// <summary>
        /// Yetenek başarıyla kullanıldığında ilgili yuvanın bekleme süresini başlatır.
        /// </summary>
        private void HandleSkillResult(int skillId, byte result, int currentMp)
        {
            // result == 0 => Success (SkillUseResult.Success).
            if (result != 0) return;

            foreach (SkillSlotUI slot in slots)
            {
                if (slot != null && slot.SkillId == skillId)
                {
                    // Bekleme süresi referans verisinden alınır; burada basit bir
                    // varsayılan (2 sn) kullanılır (gerçek değer skill cache'inden gelmeli).
                    slot.StartCooldown(2f);
                    break;
                }
            }
        }

        /// <summary>Oyuncunun o an seçili olan hedefinin UID'si (yoksa 0).</summary>
        private uint GetCurrentTargetId()
        {
            return LocalPlayer.Instance != null ? LocalPlayer.Instance.SelectedTargetId : 0u;
        }
    }
}
