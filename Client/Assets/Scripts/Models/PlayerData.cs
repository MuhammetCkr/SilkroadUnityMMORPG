namespace SROClient.Models
{
    /// <summary>
    /// Karakter/oyuncu verisi modeli. Sunucudaki SROServer.Shared.Models.Character
    /// modelinin istemci karşılığıdır. S_CHAR_LIST paketinden doldurulur.
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        /// <summary>Karakter adı.</summary>
        public string CharName;

        /// <summary>Mevcut seviye.</summary>
        public byte CurLevel;

        /// <summary>Ulaşılan en yüksek seviye.</summary>
        public byte MaxLevel;

        /// <summary>Mevcut can puanı.</summary>
        public int HP;

        /// <summary>Mevcut mana puanı.</summary>
        public int MP;

        /// <summary>Dağıtılmamış stat puanı.</summary>
        public int RemainStatPoint;

        /// <summary>PVP durumu.</summary>
        public byte PVPState;

        /// <summary>Dünya pozisyonu (X, Y, Z).</summary>
        public float PositionX;
        public float PositionY;
        public float PositionZ;
    }
}
