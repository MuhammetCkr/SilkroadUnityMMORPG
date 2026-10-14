using UnityEngine;
using UnityEngine.UI;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.UI.Alchemy
{
    /// <summary>
    /// Eşya güçlendirme (alkimi) penceresi (Faz 5). Oyuncu bir eşya slotu ve bir
    /// güçlendirme taşı slotu seçer, "Güçlendir" butonuna basar; sunucuya
    /// C_ALCHEMY_REQUEST gönderilir ve S_ALCHEMY_RESULT ile sonuç gösterilir.
    /// </summary>
    public sealed class AlchemyUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text itemSlotLabel;
        [SerializeField] private Text stoneSlotLabel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Button enhanceButton;

        private int _itemSlot = -1;
        private int _stoneSlot = -1;

        private void Start()
        {
            PacketHandler ph = NetworkManager.Instance.PacketHandler;
            ph.Register(PacketOpcodes.S_ALCHEMY_RESULT, HandleAlchemyResult);

            if (enhanceButton != null)
                enhanceButton.onClick.AddListener(RequestEnhance);

            if (panel != null)
                panel.SetActive(false);
        }

        /// <summary>Pencereyi açar/kapatır.</summary>
        public void Toggle()
        {
            if (panel != null)
                panel.SetActive(!panel.activeSelf);
        }

        /// <summary>Güçlendirilecek eşyanın envanter slotunu ayarlar.</summary>
        public void SetItemSlot(int slot)
        {
            _itemSlot = slot;
            if (itemSlotLabel != null) itemSlotLabel.text = $"Eşya: {slot}";
        }

        /// <summary>Kullanılacak güçlendirme taşının envanter slotunu ayarlar.</summary>
        public void SetStoneSlot(int slot)
        {
            _stoneSlot = slot;
            if (stoneSlotLabel != null) stoneSlotLabel.text = $"Taş: {slot}";
        }

        /// <summary>C_ALCHEMY_REQUEST: seçili eşya + taş ile güçlendirme isteği.</summary>
        public async void RequestEnhance()
        {
            if (_itemSlot < 0 || _stoneSlot < 0)
            {
                if (resultLabel != null) resultLabel.text = "Önce eşya ve taş seçin.";
                return;
            }

            byte[] packet = new PacketBuilder(PacketOpcodes.C_ALCHEMY_REQUEST)
                .WriteByte((byte)_itemSlot)
                .WriteByte((byte)_stoneSlot)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>
        /// S_ALCHEMY_RESULT: [itemId(long)] [success(byte)] [newOptLevel(byte)] [destroyed(byte)].
        /// </summary>
        private void HandleAlchemyResult(PacketReader reader)
        {
            long itemId = reader.ReadLong();
            bool success = reader.ReadByte() == 1;
            byte newOptLevel = reader.ReadByte();
            bool destroyed = reader.ReadByte() == 1;

            if (resultLabel != null)
            {
                if (destroyed)
                    resultLabel.text = "Başarısız! Eşya yok oldu.";
                else if (success)
                    resultLabel.text = $"Başarılı! Yeni seviye: +{newOptLevel}";
                else
                    resultLabel.text = "Başarısız. Eşya korundu.";
            }

            Debug.Log($"[Alchemy] itemId={itemId} success={success} +{newOptLevel} destroyed={destroyed}");
        }
    }
}
