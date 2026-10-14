using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.UI.Guild
{
    /// <summary>
    /// Lonca (guild) penceresi (Faz 5). Lonca adını, seviyesini ve üye listesini
    /// (ad + rütbe) gösterir; lonca oluşturma isteğini sunucuya gönderir.
    /// Sunucudan gelen S_GUILD_DATA paketiyle içerik tazelenir.
    /// </summary>
    public sealed class GuildUI : MonoBehaviour
    {
        /// <summary>Bir lonca üyesinin istemci tarafı görünümü.</summary>
        public sealed class GuildMemberView
        {
            public string Name;
            public byte Rank; // 0=Üye, 1=Subay, 2=Lider
        }

        [SerializeField] private GameObject panel;
        [SerializeField] private Text guildNameLabel;
        [SerializeField] private Text guildLevelLabel;
        [SerializeField] private Transform memberListRoot;
        [SerializeField] private GameObject memberRowPrefab;
        [SerializeField] private InputField createNameInput;
        [SerializeField] private Button createButton;

        private int _guildId;
        private readonly List<GuildMemberView> _members = new List<GuildMemberView>();

        // Rütbe kimliğinden görünen ada eşleme.
        private static readonly string[] RankNames = { "Üye", "Subay", "Lider" };

        private void Start()
        {
            PacketHandler ph = NetworkManager.Instance.PacketHandler;
            ph.Register(PacketOpcodes.S_GUILD_DATA, HandleGuildData);

            if (createButton != null)
                createButton.onClick.AddListener(RequestCreateFromInput);

            if (panel != null)
                panel.SetActive(false);
        }

        /// <summary>Pencereyi açar/kapatır.</summary>
        public void Toggle()
        {
            if (panel != null)
                panel.SetActive(!panel.activeSelf);
        }

        /// <summary>Giriş alanındaki ada göre lonca oluşturma isteği gönderir.</summary>
        public void RequestCreateFromInput()
        {
            string name = createNameInput != null ? createNameInput.text : string.Empty;
            RequestCreateGuild(name);
        }

        /// <summary>C_GUILD_CREATE: [guildName(string)] yeni lonca oluşturma isteği.</summary>
        public async void RequestCreateGuild(string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName)) return;

            byte[] packet = new PacketBuilder(PacketOpcodes.C_GUILD_CREATE)
                .WriteString(guildName)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>
        /// S_GUILD_DATA: [guildId(int)] [guildName(string)] [level(byte)] [memberCount(byte)]
        /// her üye için: [name(string)] [rank(byte)].
        /// </summary>
        private void HandleGuildData(PacketReader reader)
        {
            _guildId = reader.ReadInt();
            string guildName = reader.ReadString();
            byte level = reader.ReadByte();
            byte memberCount = reader.ReadByte();

            _members.Clear();
            for (int i = 0; i < memberCount; i++)
            {
                _members.Add(new GuildMemberView
                {
                    Name = reader.ReadString(),
                    Rank = reader.ReadByte()
                });
            }

            if (guildNameLabel != null) guildNameLabel.text = guildName;
            if (guildLevelLabel != null) guildLevelLabel.text = $"Seviye {level}";

            RefreshView();
            Debug.Log($"[Guild] Lonca güncellendi: id={_guildId} ad={guildName} üye={_members.Count}");
        }

        /// <summary>Üye listesini UI satırları olarak yeniden çizer.</summary>
        private void RefreshView()
        {
            if (panel != null) panel.SetActive(true);
            if (memberListRoot == null || memberRowPrefab == null) return;

            foreach (Transform child in memberListRoot)
                Destroy(child.gameObject);

            foreach (GuildMemberView m in _members)
            {
                GameObject row = Instantiate(memberRowPrefab, memberListRoot);
                Text label = row.GetComponentInChildren<Text>();
                if (label != null)
                {
                    string rank = m.Rank < RankNames.Length ? RankNames[m.Rank] : "Üye";
                    label.text = $"{m.Name}  [{rank}]";
                }
            }
        }
    }
}
