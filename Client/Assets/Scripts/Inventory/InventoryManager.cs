using System;
using UnityEngine;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.Inventory
{
    /// <summary>
    /// İstemci envanter durumunu tutan ve sunucu ile senkronize eden singleton.
    /// Sunucudan gelen S_INVENTORY_DATA / S_INVENTORY_UPDATE / S_ITEM_USE_RESULT /
    /// S_SHOP_*_RESULT paketlerini işler; taşıma/kullanma isteklerini gönderir.
    /// UI katmanı olaylara (event) abone olarak günceleme alır.
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static InventoryManager Instance { get; private set; }

        /// <summary>Toplam envanter slot sayısı (Silkroad: 13 ekipman + 96 çanta + rezerv = 112).</summary>
        public const int SlotCount = 112;

        private readonly InventorySlotData[] _slots = new InventorySlotData[SlotCount];

        /// <summary>Oyuncunun güncel altını.</summary>
        public long Gold { get; private set; }

        // --- Olaylar (UI abone olur) ---

        /// <summary>Bir slot güncellendiğinde (yeni eşya) tetiklenir.</summary>
        public event Action<int, InventorySlotData> OnSlotUpdated;

        /// <summary>Bir slot boşaldığında tetiklenir.</summary>
        public event Action<int> OnSlotCleared;

        /// <summary>Altın miktarı değiştiğinde tetiklenir.</summary>
        public event Action<long> OnGoldUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new InventorySlotData { SlotIndex = i, ItemId = 0 };
        }

        private void Start()
        {
            // Ağ paket işleyicilerini kaydet (EntityManager ile aynı desen).
            PacketHandler ph = NetworkManager.Instance.PacketHandler;
            ph.Register(PacketOpcodes.S_INVENTORY_DATA, HandleInventoryData);
            ph.Register(PacketOpcodes.S_INVENTORY_UPDATE, HandleSlotUpdate);
            ph.Register(PacketOpcodes.S_ITEM_USE_RESULT, HandleItemUseResult);
            ph.Register(PacketOpcodes.S_ITEM_DROP_RESULT, HandleItemDropResult);
            ph.Register(PacketOpcodes.S_SHOP_BUY_RESULT, HandleShopBuyResult);
            ph.Register(PacketOpcodes.S_SHOP_SELL_RESULT, HandleShopSellResult);
        }

        // ==================== Gelen paket işleyicileri ====================

        /// <summary>
        /// S_INVENTORY_DATA: [count(short)] her slot için tam eşya alanları.
        /// Tüm envanteri sıfırlayıp yeniden doldurur.
        /// </summary>
        public void HandleInventoryData(PacketReader reader)
        {
            // Önce tümünü boşalt.
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    _slots[i] = new InventorySlotData { SlotIndex = i, ItemId = 0 };
                    OnSlotCleared?.Invoke(i);
                }
            }

            ushort count = reader.ReadShort();
            for (int i = 0; i < count; i++)
            {
                byte slot = reader.ReadByte();
                InventorySlotData data = ReadItemFields(slot, reader);
                if (slot < SlotCount)
                {
                    _slots[slot] = data;
                    OnSlotUpdated?.Invoke(slot, data);
                }
            }
        }

        /// <summary>
        /// S_INVENTORY_UPDATE: [slot(byte)] [hasItem(byte)] eşya varsa alanlar.
        /// </summary>
        public void HandleSlotUpdate(PacketReader reader)
        {
            byte slot = reader.ReadByte();
            byte hasItem = reader.ReadByte();

            if (slot >= SlotCount) return;

            if (hasItem == 1)
            {
                InventorySlotData data = ReadItemFields(slot, reader, includeSlot: false);
                _slots[slot] = data;
                OnSlotUpdated?.Invoke(slot, data);
            }
            else
            {
                _slots[slot] = new InventorySlotData { SlotIndex = slot, ItemId = 0 };
                OnSlotCleared?.Invoke(slot);
            }
        }

        /// <summary>S_ITEM_USE_RESULT: [slot(byte)] [result(byte)] [hp(int)] [mp(int)].</summary>
        private void HandleItemUseResult(PacketReader reader)
        {
            byte slot = reader.ReadByte();
            byte result = reader.ReadByte();
            int hp = reader.ReadInt();
            int mp = reader.ReadInt();
            Debug.Log($"[Inventory] Eşya kullanım sonucu slot={slot} result={result} HP={hp} MP={mp}");
        }

        /// <summary>S_ITEM_DROP_RESULT: [slot(byte)] [result(byte)].</summary>
        private void HandleItemDropResult(PacketReader reader)
        {
            byte slot = reader.ReadByte();
            byte result = reader.ReadByte();
            Debug.Log($"[Inventory] Eşya düşürme sonucu slot={slot} result={result}");
        }

        /// <summary>S_SHOP_BUY_RESULT: [result(byte)] [gold(long)].</summary>
        private void HandleShopBuyResult(PacketReader reader)
        {
            byte result = reader.ReadByte();
            long gold = reader.ReadLong();
            SetGold(gold);
            Debug.Log($"[Inventory] Satın alma sonucu result={result} gold={gold}");
        }

        /// <summary>S_SHOP_SELL_RESULT: [slot(byte)] [result(byte)] [gold(long)].</summary>
        private void HandleShopSellResult(PacketReader reader)
        {
            byte slot = reader.ReadByte();
            byte result = reader.ReadByte();
            long gold = reader.ReadLong();
            SetGold(gold);
            Debug.Log($"[Inventory] Satma sonucu slot={slot} result={result} gold={gold}");
        }

        // ==================== Dışa açık sorgular ====================

        /// <summary>Belirtilen slotun verisini döner (indeks geçersizse null).</summary>
        public InventorySlotData GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
        }

        /// <summary>Slot boş mu?</summary>
        public bool IsSlotEmpty(int index)
        {
            InventorySlotData slot = GetSlot(index);
            return slot == null || slot.IsEmpty;
        }

        // ==================== Sunucuya istek gönderme ====================

        /// <summary>C_INVENTORY_MOVE: eşyayı kaynak slottan hedef slota taşıma isteği.</summary>
        public async void RequestMoveItem(int fromSlot, int toSlot)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_INVENTORY_MOVE)
                .WriteByte((byte)fromSlot)
                .WriteByte((byte)toSlot)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>C_ITEM_USE: bir slottaki eşyayı kullanma isteği.</summary>
        public async void RequestUseItem(int slot)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_ITEM_USE)
                .WriteByte((byte)slot)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>C_ITEM_DROP: bir slottaki eşyayı düşürme isteği.</summary>
        public async void RequestDropItem(int slot)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_ITEM_DROP)
                .WriteByte((byte)slot)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        // ==================== Yardımcılar ====================

        private void SetGold(long gold)
        {
            Gold = gold;
            OnGoldUpdated?.Invoke(gold);
        }

        /// <summary>
        /// Sunucudaki InventoryService.WriteItemFields ile aynı sıradaki eşya alanlarını okur.
        /// [itemId(long)] [refItemId(int)] [name(string)] [optLevel(byte)] [quantity(int)]
        /// [typeId1/2/3(int)] [reqLevel(byte)] [weight(int)].
        /// </summary>
        private static InventorySlotData ReadItemFields(byte slot, PacketReader reader, bool includeSlot = true)
        {
            var data = new InventorySlotData { SlotIndex = slot };
            data.ItemId = reader.ReadLong();
            data.RefItemId = reader.ReadInt();
            data.Name = reader.ReadString();
            data.OptLevel = reader.ReadByte();
            data.Quantity = reader.ReadInt();
            data.TypeId1 = reader.ReadInt();
            data.TypeId2 = reader.ReadInt();
            data.TypeId3 = reader.ReadInt();
            data.RequiredLevel = reader.ReadByte();
            data.Weight = reader.ReadInt();
            data.CodeName = data.Name;
            return data;
        }
    }
}
