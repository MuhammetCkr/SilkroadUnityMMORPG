using UnityEngine;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.Gameplay
{
    /// <summary>
    /// Yerel oyuncunun hareketini yöneten bileşen. WASD ile yönsel hareket ve
    /// sol tık ile "tıkla-git" (click-to-move) desteği sağlar. Hareket, Unity'nin
    /// CharacterController bileşeni ile uygulanır; hedef pozisyon değişince sunucuya
    /// C_MOVE_REQUEST gönderilir (küçük değişimler eşiklenerek/throttle ile atlanır).
    ///
    /// Not: Sınıf adı Unity'nin yerleşik CharacterController'ı ile karışmasın diye
    /// bilinçli olarak CharacterMovementController seçilmiştir.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMovementController : MonoBehaviour
    {
        [Header("Hareket Ayarları")]
        [Tooltip("Hareket hızı (birim/saniye)")]
        public float MoveSpeed = 7f;

        [Tooltip("Dönüş hızı (Slerp yumuşatma katsayısı)")]
        public float RotationSpeed = 10f;

        [Tooltip("Yerçekimi ivmesi")]
        public float Gravity = -20f;

        [Tooltip("Sunucuya hareket bildirimi için minimum konum değişimi (birim)")]
        public float MoveSendThreshold = 0.1f;

        [Tooltip("Tıkla-git hedefine varmış sayılacak mesafe")]
        public float ArriveThreshold = 0.2f;

        private CharacterController _controller;
        private Camera _mainCamera;

        // Tıkla-git hedefi (varsa).
        private Vector3? _clickTarget;

        // Sunucuya en son bildirilen konum.
        private Vector3 _lastSentPosition;

        // Dikey hız (yerçekimi için).
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
            _lastSentPosition = transform.position;
        }

        private void Update()
        {
            HandleInput();
            ApplyMovement();
            TrySendMovement();
        }

        /// <summary>
        /// Girişi okur: WASD anlık yön verirse tıkla-git iptal olur; sol tık yeni hedef koyar.
        /// </summary>
        private void HandleInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                // Klavye girişi tıkla-git hedefini geçersiz kılar.
                _clickTarget = null;
                return;
            }

            // Sol tık ile zemin üzerinde hedef belirle.
            if (Input.GetMouseButtonDown(0) && _mainCamera != null)
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    _clickTarget = hit.point;
                }
            }
        }

        /// <summary>
        /// Girişe göre hareketi uygular (WASD öncelikli, yoksa tıkla-git hedefine ilerler).
        /// </summary>
        private void ApplyMovement()
        {
            Vector3 horizontalMove = Vector3.zero;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                // Kameraya göreli yönsel hareket.
                Vector3 dir = new Vector3(h, 0f, v).normalized;
                horizontalMove = dir * MoveSpeed;
                RotateTowards(dir);
            }
            else if (_clickTarget.HasValue)
            {
                Vector3 toTarget = _clickTarget.Value - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude <= ArriveThreshold)
                {
                    _clickTarget = null; // Hedefe varıldı.
                }
                else
                {
                    Vector3 dir = toTarget.normalized;
                    horizontalMove = dir * MoveSpeed;
                    RotateTowards(dir);
                }
            }

            // Yerçekimi.
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -1f;
            else
                _verticalVelocity += Gravity * Time.deltaTime;

            Vector3 motion = horizontalMove;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        /// <summary>Verilen yatay yöne doğru karakteri yumuşakça döndürür.</summary>
        private void RotateTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, RotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Konum, eşik değerinden fazla değiştiyse sunucuya C_MOVE_REQUEST gönderir.
        /// </summary>
        private void TrySendMovement()
        {
            Vector3 pos = transform.position;
            if ((pos - _lastSentPosition).sqrMagnitude < MoveSendThreshold * MoveSendThreshold)
                return;

            _lastSentPosition = pos;
            SendMoveRequest(pos);
        }

        /// <summary>Hedef pozisyonu C_MOVE_REQUEST paketiyle sunucuya iletir.</summary>
        private void SendMoveRequest(Vector3 target)
        {
            NetworkManager net = NetworkManager.Instance;
            if (net == null || !net.Connection.IsConnected)
                return;

            byte[] packet = new PacketBuilder(PacketOpcodes.C_MOVE_REQUEST)
                .WriteFloat(target.x)
                .WriteFloat(target.y)
                .WriteFloat(target.z)
                .Build();

            _ = net.SendAsync(packet);
        }
    }
}
