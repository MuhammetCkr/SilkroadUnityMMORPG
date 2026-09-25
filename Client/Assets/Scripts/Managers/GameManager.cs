using System.Collections.Generic;
using SROClient.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SROClient.Managers
{
    /// <summary>
    /// Oyunun genel akışını ve sahne geçişlerini yöneten singleton.
    /// Akış: Login -> CharacterSelect -> Game.
    /// Oturum tokenı ve seçili karakter gibi global durumu tutar.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static GameManager Instance { get; private set; }

        // --- Sahne adları (Build Settings'te tanımlı olmalıdır) ---
        public const string SceneLogin = "Login";
        public const string SceneCharacterSelect = "CharacterSelect";
        public const string SceneGame = "Game";

        /// <summary>Giriş sonrası alınan oturum tokenı.</summary>
        public string SessionToken { get; set; }

        /// <summary>Giriş yapan hesabın kimliği (JID).</summary>
        public int AccountJid { get; set; }

        /// <summary>Sunucudan gelen karakter listesi.</summary>
        public List<PlayerData> Characters { get; set; } = new List<PlayerData>();

        /// <summary>Oyuncunun seçtiği karakter.</summary>
        public PlayerData SelectedCharacter { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Karakter seçim sahnesine geçer.</summary>
        public void GoToCharacterSelect() => SceneManager.LoadScene(SceneCharacterSelect);

        /// <summary>Oyun sahnesine geçer (Faz 2'de dünya yüklenecek).</summary>
        public void GoToGame() => SceneManager.LoadScene(SceneGame);

        /// <summary>Giriş sahnesine döner (ör. bağlantı koptuğunda).</summary>
        public void GoToLogin() => SceneManager.LoadScene(SceneLogin);
    }
}
