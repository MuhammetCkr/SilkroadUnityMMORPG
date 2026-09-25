using UnityEngine;
using UnityEngine.UI;

namespace SROClient.UI.Inventory
{
    /// <summary>
    /// Envanter sürükle-bırak işlemini yöneten singleton. Sürüklenen eşyanın
    /// ikonunu imlecin altında gösterir ve kaynağı takip eder.
    /// </summary>
    public sealed class DragDropHandler : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static DragDropHandler Instance { get; private set; }

        [Header("Sürükleme görseli")]
        [Tooltip("Fareyi takip eden sürükleme ikonu")]
        [SerializeField] private Image dragIcon;

        /// <summary>Sürükleme başladığında kaynak slot.</summary>
        public InventorySlotUI CurrentSource { get; private set; }

        private bool _isDragging;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (dragIcon != null)
                dragIcon.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Sürükleme aktifken ikon imleci takip eder.
            if (_isDragging && dragIcon != null)
                dragIcon.transform.position = Input.mousePosition;
        }

        /// <summary>Sürüklemeyi başlatır: kaynağı ve ikonu ayarlar.</summary>
        public void BeginDrag(InventorySlotUI source, Sprite icon)
        {
            CurrentSource = source;
            _isDragging = true;

            if (dragIcon != null)
            {
                dragIcon.sprite = icon;
                dragIcon.gameObject.SetActive(true);
                dragIcon.transform.position = Input.mousePosition;
            }
        }

        /// <summary>Sürüklemeyi bitirir: ikonu gizler ve kaynağı temizler.</summary>
        public void EndDrag()
        {
            _isDragging = false;
            CurrentSource = null;

            if (dragIcon != null)
            {
                dragIcon.sprite = null;
                dragIcon.gameObject.SetActive(false);
            }
        }
    }
}
