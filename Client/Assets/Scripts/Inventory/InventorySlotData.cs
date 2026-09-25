namespace SROClient.Inventory
{
    /// <summary>
    /// İstemci tarafındaki tek bir envanter slotunun veri modeli.
    /// Sunucudan gelen S_INVENTORY_DATA / S_INVENTORY_UPDATE paketleriyle doldurulur.
    /// ItemId 0 ise slot boştur.
    /// </summary>
    [System.Serializable]
    public class InventorySlotData
    {
        /// <summary>Slot indeksi (0 tabanlı).</summary>
        public int SlotIndex;

        /// <summary>Eşyanın benzersiz veritabanı kimliği (0 ise boş).</summary>
        public long ItemId;

        /// <summary>Referans eşya kimliği (şablon).</summary>
        public int RefItemId;

        /// <summary>Görünen ad.</summary>
        public string Name;

        /// <summary>Referans kod adı.</summary>
        public string CodeName;

        /// <summary>Yükseltme (plus) seviyesi.</summary>
        public byte OptLevel;

        /// <summary>Yığın adedi.</summary>
        public int Quantity;

        // Tip bilgisi (ikon/kategori belirleme için).
        public int TypeId1;
        public int TypeId2;
        public int TypeId3;

        /// <summary>Kullanım için gereken seviye.</summary>
        public byte RequiredLevel;

        /// <summary>Ağırlık.</summary>
        public int Weight;

        /// <summary>Slot boş mu?</summary>
        public bool IsEmpty => ItemId == 0;
    }
}
