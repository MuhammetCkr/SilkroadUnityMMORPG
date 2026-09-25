using UnityEngine;
using UnityEngine.UI;
using SROClient.Inventory;

namespace SROClient.UI.Inventory
{
    /// <summary>
    /// Envanter penceresini yöneten UI bileşeni. I tuşu ile açılıp kapanır.
    /// InventoryManager olaylarına abone olur ve slotları, altını, ağırlığı günceller.
    /// </summary>
    public sealed class InventoryUI : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Açılıp kapanan envanter paneli kök nesnesi")]
        [SerializeField] private GameObject panel;

        [Header("Slotlar")]
        [Tooltip("112 envanter slot bileşeni (sıralı)")]
        [SerializeField] private InventorySlotUI[] slots = new InventorySlotUI[InventoryManager.SlotCount];

        [Header("Bilgi metinleri")]
        [SerializeField] private Text goldText;
        [SerializeField] private Text weightText;

        [Header("Açma/Kapama tuşu")]
        [SerializeField] private KeyCode toggleKey = KeyCode.I;

        private bool _isOpen;

        private void Start()
        {
            // Slot indekslerini başlat.
            for (int i = 0; i < slots.Length; i++)
                slots[i]?.Initialize(i);

            // InventoryManager olaylarına abone ol.
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.OnSlotUpdated += RefreshSlot;
                inv.OnSlotCleared += ClearSlot;
                inv.OnGoldUpdated += UpdateGold;
            }

            SetOpen(false);
        }

        private void OnDestroy()
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.OnSlotUpdated -= RefreshSlot;
                inv.OnSlotCleared -= ClearSlot;
                inv.OnGoldUpdated -= UpdateGold;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                SetOpen(!_isOpen);
        }

        /// <summary>Paneli açar/kapatır.</summary>
        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (panel != null) panel.SetActive(open);
        }

        /// <summary>Bir slotun görselini verilen veriyle günceller.</summary>
        public void RefreshSlot(int index, InventorySlotData data)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index]?.SetData(data);
            UpdateWeight();
        }

        /// <summary>Bir slotu boşaltır.</summary>
        public void ClearSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index]?.Clear();
            UpdateWeight();
        }

        /// <summary>Altın metnini günceller.</summary>
        public void UpdateGold(long gold)
        {
            if (goldText != null)
                goldText.text = $"{gold:N0} Gold";
        }

        /// <summary>Toplam ağırlığı hesaplayıp metni günceller.</summary>
        public void UpdateWeight()
        {
            if (weightText == null) return;

            int total = 0;
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null)
            {
                for (int i = 0; i < InventoryManager.SlotCount; i++)
                {
                    InventorySlotData s = inv.GetSlot(i);
                    if (s != null && !s.IsEmpty)
                        total += s.Weight * Mathf.Max(1, s.Quantity);
                }
            }
            weightText.text = $"{total} g";
        }
    }
}
