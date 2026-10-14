using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.UI.Party
{
    /// <summary>
    /// Grup (party) penceresi (Faz 5). Grup üyelerini (ad, seviye, HP/MP) listeler;
    /// grup oluşturma ve davet isteklerini sunucuya gönderir. Sunucudan gelen
    /// S_PARTY_DATA / S_PARTY_UPDATE paketleriyle üye listesini tazeler.
    /// </summary>
    public sealed class PartyUI : MonoBehaviour
    {
        /// <summary>Bir grup üyesinin istemci tarafı görünümü.</summary>
        public sealed class PartyMemberData
        {
            public uint UniqueId;
            public string Name;
            public byte Level;
            public int HP;
            public int MaxHP;
            public int MP;
            public int MaxMP;
        }

        [SerializeField] private GameObject panel;
        [SerializeField] private Transform memberListRoot; // üye satırlarının ebeveyni
        [SerializeField] private GameObject memberRowPrefab;

        private int _partyId;
        private readonly List<PartyMemberData> _members = new List<PartyMemberData>();

        /// <summary>Güncel grup üyeleri (salt okunur erişim).</summary>
        public IReadOnlyList<PartyMemberData> Members => _members;

        private void Start()
        {
            PacketHandler ph = NetworkManager.Instance.PacketHandler;
            ph.Register(PacketOpcodes.S_PARTY_DATA, HandlePartyData);
            ph.Register(PacketOpcodes.S_PARTY_UPDATE, HandlePartyData);

            if (panel != null)
                panel.SetActive(false);
        }

        /// <summary>C_PARTY_CREATE: yeni grup oluşturma isteği.</summary>
        public async void RequestCreateParty()
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_PARTY_CREATE).Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>C_PARTY_INVITE: bir oyuncuyu gruba davet etme isteği.</summary>
        public async void RequestInvite(uint targetId)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_PARTY_INVITE)
                .WriteInt((int)targetId)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>
        /// S_PARTY_DATA: [partyId(int)] [memberCount(byte)] her üye için:
        /// [uniqueId(int)] [name(string)] [level(byte)] [hp(int)] [maxHp(int)] [mp(int)] [maxMp(int)].
        /// </summary>
        private void HandlePartyData(PacketReader reader)
        {
            _partyId = reader.ReadInt();
            byte count = reader.ReadByte();

            _members.Clear();
            for (int i = 0; i < count; i++)
            {
                _members.Add(new PartyMemberData
                {
                    UniqueId = (uint)reader.ReadInt(),
                    Name = reader.ReadString(),
                    Level = reader.ReadByte(),
                    HP = reader.ReadInt(),
                    MaxHP = reader.ReadInt(),
                    MP = reader.ReadInt(),
                    MaxMP = reader.ReadInt()
                });
            }

            RefreshView();
            Debug.Log($"[Party] Grup güncellendi: id={_partyId} üye={_members.Count}");
        }

        /// <summary>Üye listesini UI satırları olarak yeniden çizer.</summary>
        private void RefreshView()
        {
            if (panel != null) panel.SetActive(_members.Count > 0);
            if (memberListRoot == null || memberRowPrefab == null) return;

            // Mevcut satırları temizle.
            foreach (Transform child in memberListRoot)
                Destroy(child.gameObject);

            // Her üye için bir satır oluştur.
            foreach (PartyMemberData m in _members)
            {
                GameObject row = Instantiate(memberRowPrefab, memberListRoot);
                Text label = row.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{m.Name} (Sv.{m.Level})  HP {m.HP}/{m.MaxHP}";
            }
        }
    }
}
