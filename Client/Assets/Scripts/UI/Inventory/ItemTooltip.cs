using UnityEngine;
using UnityEngine.UI;
using SROClient.Inventory;

namespace SROClient.UI.Inventory
{
    /// <summary>
    /// Eşya üzerine gelindiğinde ad, tip, statlar ve fiyatı gösteren tooltip singleton'ı.
    /// </summary>
    public sealed class ItemTooltip : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static ItemTooltip Instance { get; private set; }

        [Header("Panel ve metinler")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text typeText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text priceText;

        [Tooltip("Tooltip'in imleçten kayma miktarı")]
        [SerializeField] private Vector2 offset = new Vector2(16f, -16f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Hide();
        }

        /// <summary>Tooltip'i verilen eşya bilgisiyle belirtilen ekran konumunda gösterir.</summary>
        public void Show(InventorySlotData data, Vector3 position)
        {
            if (data == null || data.IsEmpty) { Hide(); return; }

            if (nameText != null)
                nameText.text = data.OptLevel > 0 ? $"+{data.OptLevel} {data.Name}" : data.Name;

            if (typeText != null)
                typeText.text = $"Tip: {data.TypeId1}/{data.TypeId2}/{data.TypeId3}";

            if (statsText != null)
            {
                string reqLevel = data.RequiredLevel > 0 ? $"Gerekli Seviye: {data.RequiredLevel}\n" : string.Empty;
                statsText.text = $"{reqLevel}Adet: {data.Quantity}\nAğırlık: {data.Weight}";
            }

            if (priceText != null)
                priceText.text = string.Empty; // Fiyat bilgisi dükkan bağlamında ayrıca gösterilir.

            if (panel != null)
            {
                panel.transform.position = position + (Vector3)offset;
                panel.SetActive(true);
            }
        }

        /// <summary>Tooltip'i gizler.</summary>
        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }
}
