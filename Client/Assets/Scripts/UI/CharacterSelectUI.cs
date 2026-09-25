using System.Collections.Generic;
using SROClient.Managers;
using SROClient.Models;
using SROClient.Network;
using UnityEngine;
using UnityEngine.UI;

namespace SROClient.UI
{
    /// <summary>
    /// Karakter seçim ekranı denetleyicisi.
    /// Girişten sonra C_CHAR_LIST_REQ gönderir, S_CHAR_LIST paketini parse edip
    /// karakterleri listeler; seçilen karakterle C_SELECT_CHAR gönderir.
    /// </summary>
    public sealed class CharacterSelectUI : MonoBehaviour
    {
        [Header("UI Referansları")]
        [Tooltip("Karakter satırlarının ekleneceği içerik alanı (dikey layout)")]
        [SerializeField] private Transform characterListContent;

        [Tooltip("Her karakter için klonlanacak buton şablonu")]
        [SerializeField] private Button characterEntryTemplate;

        [SerializeField] private Text statusText;

        private PlayerData _selected;

        private void Start()
        {
            var handler = NetworkManager.Instance.PacketHandler;
            handler.Register(PacketOpcodes.S_CHAR_LIST, OnCharListReceived);
            handler.Register(PacketOpcodes.S_SELECT_CHAR_OK, OnSelectCharOk);

            RequestCharacterList();
        }

        /// <summary>Sunucudan karakter listesini ister (token ile).</summary>
        private async void RequestCharacterList()
        {
            SetStatus("Karakterler yükleniyor...");
            byte[] packet = new PacketBuilder(PacketOpcodes.C_CHAR_LIST_REQ)
                .WriteString(GameManager.Instance.SessionToken)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>S_CHAR_LIST: karakter sayısı ve her karakterin alanları parse edilir.</summary>
        private void OnCharListReceived(PacketReader reader)
        {
            byte count = reader.ReadByte();
            var characters = new List<PlayerData>();

            for (int i = 0; i < count; i++)
            {
                var c = new PlayerData
                {
                    CharName = reader.ReadString(),
                    CurLevel = reader.ReadByte(),
                    MaxLevel = reader.ReadByte(),
                    HP = reader.ReadInt(),
                    MP = reader.ReadInt(),
                    RemainStatPoint = reader.ReadInt(),
                    PVPState = reader.ReadByte()
                };
                characters.Add(c);
            }

            GameManager.Instance.Characters = characters;
            PopulateList(characters);
            SetStatus(count == 0 ? "Karakter bulunamadı." : $"{count} karakter listelendi.");
        }

        /// <summary>Karakter listesini UI'da butonlar halinde oluşturur.</summary>
        private void PopulateList(List<PlayerData> characters)
        {
            if (characterListContent == null || characterEntryTemplate == null)
                return;

            // Önceki satırları temizle (şablon hariç).
            foreach (Transform child in characterListContent)
            {
                if (child != characterEntryTemplate.transform)
                    Destroy(child.gameObject);
            }
            characterEntryTemplate.gameObject.SetActive(false);

            foreach (PlayerData c in characters)
            {
                Button entry = Instantiate(characterEntryTemplate, characterListContent);
                entry.gameObject.SetActive(true);

                Text label = entry.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{c.CharName}  (Lv.{c.CurLevel})";

                PlayerData captured = c; // Closure için kopya.
                entry.onClick.AddListener(() => OnCharacterChosen(captured));
            }
        }

        /// <summary>Bir karakter satırına tıklandığında seçim yapılır.</summary>
        private void OnCharacterChosen(PlayerData character)
        {
            _selected = character;
            GameManager.Instance.SelectedCharacter = character;
            SetStatus($"Seçilen karakter: {character.CharName}");
            SendSelect(character.CharName);
        }

        /// <summary>C_SELECT_CHAR paketini gönderir.</summary>
        private async void SendSelect(string charName)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_SELECT_CHAR)
                .WriteString(GameManager.Instance.SessionToken)
                .WriteString(charName)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>S_SELECT_CHAR_OK: seçim onaylanınca oyun sahnesine geçilir.</summary>
        private void OnSelectCharOk(PacketReader reader)
        {
            string charName = reader.ReadString();
            SetStatus($"Karakter onaylandı: {charName}. Dünyaya giriliyor...");
            // Faz 2: oyun sahnesine geçiş.
            GameManager.Instance.GoToGame();
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[CharacterSelectUI] {message}");
        }
    }
}
