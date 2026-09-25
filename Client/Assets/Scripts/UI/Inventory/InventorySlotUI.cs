using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SROClient.Inventory;

namespace SROClient.UI.Inventory
{
    /// <summary>
    /// Tek bir envanter slotunun görsel bileşeni. Sürükle-bırak, tıklama ve tooltip
    /// olaylarını işler. Ekipman ikonu, adet ve yükseltme seviyesi metinlerini gösterir.
    /// </summary>
    public sealed class InventorySlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IDropHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Görsel öğeler")]
        [Tooltip("Eşya ikonu")]
        [SerializeField] private Image itemIcon;

        [Tooltip("Yığın adedi metni")]
        [SerializeField] private Text quantityText;

        [Tooltip("Yükseltme (+N) seviyesi metni")]
        [SerializeField] private Text optLevelText;

        [Tooltip("Boş slot için karartma katmanı")]
        [SerializeField] private GameObject emptyOverlay;

        /// <summary>Bu slotun envanterdeki indeksi.</summary>
        public int SlotIndex { get; private set; }

        /// <summary>Slotta bulunan eşya verisi (boşsa null veya IsEmpty).</summary>
        public InventorySlotData Data { get; private set; }

        /// <summary>Slot indeksini ayarlar (UI kurulum sırasında çağrılır).</summary>
        public void Initialize(int slotIndex)
        {
            SlotIndex = slotIndex;
            Clear();
        }

        /// <summary>Slota eşya verisi yerleştirir ve görselleri günceller.</summary>
        public void SetData(InventorySlotData data)
        {
            Data = data;

            if (data == null || data.IsEmpty)
            {
                Clear();
                return;
            }

            if (itemIcon != null)
            {
                itemIcon.enabled = true;
                // Gerçek ikon atlası Faz 3 kapsamı dışında; RefItemId'ye göre yüklenecek.
                itemIcon.sprite = ResolveIcon(data.RefItemId);
            }

            if (quantityText != null)
                quantityText.text = data.Quantity > 1 ? data.Quantity.ToString() : string.Empty;

            if (optLevelText != null)
                optLevelText.text = data.OptLevel > 0 ? $"+{data.OptLevel}" : string.Empty;

            if (emptyOverlay != null)
                emptyOverlay.SetActive(false);
        }

        /// <summary>Slotu boşaltır (görselleri temizler).</summary>
        public void Clear()
        {
            Data = null;

            if (itemIcon != null)
            {
                itemIcon.sprite = null;
                itemIcon.enabled = false;
            }
            if (quantityText != null) quantityText.text = string.Empty;
            if (optLevelText != null) optLevelText.text = string.Empty;
            if (emptyOverlay != null) emptyOverlay.SetActive(true);
        }

        // ==================== Sürükle-Bırak ====================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Data == null || Data.IsEmpty) return;
            DragDropHandler.Instance?.BeginDrag(this, itemIcon != null ? itemIcon.sprite : null);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Sürüklenen ikon DragDropHandler.Update içinde fareyi takip eder.
        }

        public void OnDrop(PointerEventData eventData)
        {
            InventorySlotUI source = DragDropHandler.Instance?.CurrentSource;
            if (source != null && source.SlotIndex != SlotIndex)
            {
                InventoryManager.Instance.RequestMoveItem(source.SlotIndex, SlotIndex);
            }
            DragDropHandler.Instance?.EndDrag();
        }

        // ==================== Tooltip ====================

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Data != null && !Data.IsEmpty)
                ItemTooltip.Instance?.Show(Data, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ItemTooltip.Instance?.Hide();
        }

        // ==================== Yardımcı ====================

        private static Sprite ResolveIcon(int refItemId)
        {
            // Yer tutucu: gerçek ikon yükleme (Resources/atlas) ileride eklenecek.
            return null;
        }
    }
}
