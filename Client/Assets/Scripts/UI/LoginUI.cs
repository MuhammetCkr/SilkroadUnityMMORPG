using System;
using SROClient.Managers;
using SROClient.Network;
using UnityEngine;
using UnityEngine.UI;

namespace SROClient.UI
{
    /// <summary>
    /// Giriş ekranı denetleyicisi. Kullanıcı adı/şifre alır, C_LOGIN_REQUEST gönderir,
    /// S_LOGIN_SUCCESS alındığında karakter seçim ekranına geçer.
    /// </summary>
    public sealed class LoginUI : MonoBehaviour
    {
        [Header("UI Referansları")]
        [SerializeField] private InputField usernameInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Text statusText;

        [Header("İstemci Ayarları")]
        [SerializeField] private string clientVersion = "1.0.0";

        private void Start()
        {
            // Buton tıklama olayını bağla.
            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClick);

            // Giriş cevaplarını dinlemek için işleyicileri kaydet.
            var handler = NetworkManager.Instance.PacketHandler;
            handler.Register(PacketOpcodes.S_LOGIN_SUCCESS, OnLoginSuccess);
            handler.Register(PacketOpcodes.S_LOGIN_FAILED, OnLoginFailed);
        }

        /// <summary>Login butonuna tıklandığında sunucuya giriş isteği gönderir.</summary>
        private async void OnLoginButtonClick()
        {
            string username = usernameInput != null ? usernameInput.text : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Kullanıcı adı ve şifre giriniz.");
                return;
            }

            SetStatus("Sunucuya bağlanılıyor...");
            SetInteractable(false);

            try
            {
                // Henüz bağlı değilse bağlan.
                if (!NetworkManager.Instance.Connection.IsConnected)
                    await NetworkManager.Instance.ConnectAsync();

                // C_LOGIN_REQUEST paketini oluştur ve gönder.
                byte[] packet = new PacketBuilder(PacketOpcodes.C_LOGIN_REQUEST)
                    .WriteString(username)
                    .WriteString(password)
                    .WriteString(clientVersion)
                    .Build();

                await NetworkManager.Instance.SendAsync(packet);
                SetStatus("Giriş bilgileri gönderildi, yanıt bekleniyor...");
            }
            catch (Exception ex)
            {
                SetStatus($"Bağlantı hatası: {ex.Message}");
                SetInteractable(true);
            }
        }

        /// <summary>S_LOGIN_SUCCESS: token ve JID okunur, karakter seçime geçilir.</summary>
        private void OnLoginSuccess(PacketReader reader)
        {
            string token = reader.ReadString();
            int jid = reader.ReadInt();

            GameManager.Instance.SessionToken = token;
            GameManager.Instance.AccountJid = jid;

            SetStatus("Giriş başarılı!");
            GameManager.Instance.GoToCharacterSelect();
        }

        /// <summary>S_LOGIN_FAILED: hata kodu ve mesaj okunur, kullanıcı bilgilendirilir.</summary>
        private void OnLoginFailed(PacketReader reader)
        {
            byte errorCode = reader.ReadByte();
            string message = reader.ReadString();

            SetStatus(errorCode == 1 ? $"Hesap engelli: {message}" : $"Giriş başarısız: {message}");
            SetInteractable(true);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[LoginUI] {message}");
        }

        private void SetInteractable(bool value)
        {
            if (loginButton != null) loginButton.interactable = value;
        }
    }
}
