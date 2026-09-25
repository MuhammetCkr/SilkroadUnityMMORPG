using UnityEngine;

namespace SROClient.UI.HUD
{
    /// <summary>
    /// Basit tepeden bakışlı (top-down) mini harita denetleyicisi. Ayrı bir ortografik
    /// kamera oyuncuyu yukarıdan takip eder ve görüntüyü bir RenderTexture'a çizer;
    /// bu doku HUD üzerinde bir RawImage ile gösterilebilir.
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Takip")]
        [Tooltip("Mini haritanın takip edeceği hedef (yerel oyuncu)")]
        public Transform Target;

        [Tooltip("Kameranın hedef üzerindeki yüksekliği")]
        public float Height = 50f;

        [Header("Kamera")]
        [Tooltip("Mini harita kamerası (ortografik). Boşsa çalışma anında oluşturulur.")]
        public Camera MinimapCamera;

        [Tooltip("Ortografik görüş yarıçapı (harita zoom seviyesi)")]
        public float OrthographicSize = 30f;

        [Tooltip("Mini harita render dokusu çözünürlüğü")]
        public int TextureSize = 256;

        /// <summary>Oluşturulan mini harita dokusu (HUD RawImage'a atanabilir).</summary>
        public RenderTexture MinimapTexture { get; private set; }

        private void Start()
        {
            SetupCamera();
        }

        /// <summary>Mini harita kamerasını ve render dokusunu hazırlar.</summary>
        private void SetupCamera()
        {
            if (MinimapCamera == null)
            {
                var camObject = new GameObject("MinimapCamera");
                camObject.transform.SetParent(transform, false);
                MinimapCamera = camObject.AddComponent<Camera>();
            }

            MinimapCamera.orthographic = true;
            MinimapCamera.orthographicSize = OrthographicSize;
            MinimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Tepeden bakış.

            MinimapTexture = new RenderTexture(TextureSize, TextureSize, 16);
            MinimapCamera.targetTexture = MinimapTexture;
        }

        private void LateUpdate()
        {
            if (Target == null || MinimapCamera == null)
                return;

            // Kamerayı hedefin üstüne konumlandır (yalnızca yatay takip).
            Vector3 pos = Target.position;
            pos.y += Height;
            MinimapCamera.transform.position = pos;
        }

        private void OnDestroy()
        {
            if (MinimapTexture != null)
            {
                MinimapTexture.Release();
                MinimapTexture = null;
            }
        }
    }
}
