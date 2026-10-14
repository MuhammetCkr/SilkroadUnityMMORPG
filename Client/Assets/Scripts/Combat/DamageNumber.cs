using UnityEngine;

namespace SROClient.Combat
{
    /// <summary>
    /// Bir varlığın üzerinde kısa süre beliren yüzen hasar sayısı (Faz 4).
    /// Yukarı doğru süzülür ve zamanla saydamlaşarak yok olur. Kritik vuruşlar
    /// farklı renk/ölçekte gösterilir. Prefab yoksa yalnızca log üretir (güvenli no-op).
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        /// <summary>Hasar sayısı prefab'ı (Inspector'dan atanır). Null ise görsel oluşturulmaz.</summary>
        public static GameObject Prefab;

        [SerializeField] private float lifetime = 1.0f;
        [SerializeField] private float floatSpeed = 1.5f;

        private float _age;
        private TextMesh _text;
        private Color _baseColor = Color.white;

        /// <summary>
        /// Belirtilen hedef varlığın konumunda yüzen bir hasar sayısı oluşturur.
        /// Prefab atanmamışsa yalnızca bilgi loglanır.
        /// </summary>
        public static void Spawn(uint targetId, int damage, bool isCritical)
        {
            if (Prefab == null)
            {
                Debug.Log($"[DamageNumber] UID={targetId} hasar={damage} kritik={isCritical}");
                return;
            }

            GameObject go = Instantiate(Prefab);
            var number = go.GetComponent<DamageNumber>();
            if (number != null)
                number.Initialize(damage, isCritical);
        }

        /// <summary>Hasar değeri ve kritik durumuna göre metni ve rengi ayarlar.</summary>
        public void Initialize(int damage, bool isCritical)
        {
            _text = GetComponent<TextMesh>();
            if (_text != null)
            {
                _text.text = isCritical ? $"{damage}!" : damage.ToString();
                _baseColor = isCritical ? new Color(1f, 0.8f, 0.1f) : Color.white;
                _text.color = _baseColor;
                transform.localScale *= isCritical ? 1.4f : 1.0f;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;

            // Yukarı süzül.
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            // Ömrün sonuna doğru saydamlaş.
            if (_text != null)
            {
                float alpha = Mathf.Clamp01(1f - _age / lifetime);
                _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
            }

            if (_age >= lifetime)
                Destroy(gameObject);
        }
    }
}
