using UnityEngine;

namespace SROClient.Gameplay
{
    /// <summary>
    /// Silkroad tarzı üçüncü şahıs takip kamerası. Hedefi (oyuncu) yumuşakça takip eder,
    /// fare tekerleği ile yakınlaşma/uzaklaşma (zoom) ve sağ tık basılı tutarak yörünge
    /// (orbit) dönüşü sağlar.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Hedef")]
        [Tooltip("Kameranın takip edeceği hedef (genellikle yerel oyuncu)")]
        public Transform Target;

        [Tooltip("Hedefin merkezine göre bakış yüksekliği ofseti")]
        public float HeightOffset = 2f;

        [Header("Mesafe (Zoom)")]
        [Tooltip("Başlangıç mesafesi")]
        public float Distance = 15f;

        [Tooltip("En yakın mesafe")]
        public float MinDistance = 5f;

        [Tooltip("En uzak mesafe")]
        public float MaxDistance = 30f;

        [Tooltip("Tekerlek zoom hızı")]
        public float ZoomSpeed = 5f;

        [Header("Dönüş (Orbit)")]
        [Tooltip("Yatay dönüş hızı")]
        public float RotationSpeedX = 120f;

        [Tooltip("Dikey dönüş hızı")]
        public float RotationSpeedY = 80f;

        [Tooltip("Minimum dikey açı")]
        public float MinPitch = 10f;

        [Tooltip("Maksimum dikey açı")]
        public float MaxPitch = 80f;

        [Header("Yumuşatma")]
        [Tooltip("Takip yumuşatma hızı")]
        public float FollowSmooth = 10f;

        // Geçerli yörünge açıları.
        private float _yaw = 0f;
        private float _pitch = 30f;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void LateUpdate()
        {
            if (Target == null)
                return;

            HandleZoom();
            HandleOrbit();
            UpdatePosition();
        }

        /// <summary>Fare tekerleği ile mesafeyi ayarlar.</summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                Distance = Mathf.Clamp(
                    Distance - scroll * ZoomSpeed, MinDistance, MaxDistance);
            }
        }

        /// <summary>Sağ tık basılıyken fare hareketiyle kamerayı döndürür.</summary>
        private void HandleOrbit()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * RotationSpeedX * Time.deltaTime;
                _pitch -= Input.GetAxis("Mouse Y") * RotationSpeedY * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            }
        }

        /// <summary>Açı ve mesafeye göre kamera konumunu ve bakışını günceller.</summary>
        private void UpdatePosition()
        {
            Vector3 focusPoint = Target.position + Vector3.up * HeightOffset;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * Distance;

            // Yumuşak takip.
            transform.position = Vector3.Lerp(
                transform.position, desiredPosition, FollowSmooth * Time.deltaTime);
            transform.LookAt(focusPoint);
        }
    }
}
