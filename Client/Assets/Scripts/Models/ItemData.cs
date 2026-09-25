namespace SROClient.Models
{
    /// <summary>
    /// Eşya (item) verisi modeli. SRO_VT_SHARD._Items tablosundan gelecek verileri
    /// temsil eder. Faz 3 (Envanter & Item) için hazır tutulan iskelet modeldir.
    /// </summary>
    [System.Serializable]
    public class ItemData
    {
        /// <summary>Eşyanın benzersiz kimliği (item instance ID).</summary>
        public long ItemId;

        /// <summary>Eşya tanım referansı (RefItemID — item şablonu).</summary>
        public int RefItemId;

        /// <summary>Eşya adı.</summary>
        public string Name;

        /// <summary>Yığın adedi (stack count).</summary>
        public int Quantity;

        /// <summary>Envanter içindeki slot numarası.</summary>
        public byte Slot;

        /// <summary>Geliştirme/artı seviyesi (+0, +1, ...).</summary>
        public byte OptLevel;

        /// <summary>Kalan dayanıklılık (durability).</summary>
        public int Durability;
    }
}
