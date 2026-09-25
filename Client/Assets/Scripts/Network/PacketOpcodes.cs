namespace SROClient.Network
{
    /// <summary>
    /// İstemci-sunucu paket işlem kodları. Sunucudaki
    /// SROServer.Shared.Constants.PacketOpcodes ile BİREBİR aynı olmalıdır.
    /// C_ = istemciden sunucuya, S_ = sunucudan istemciye.
    /// </summary>
    public static class PacketOpcodes
    {
        // --- Auth opcode'ları ---
        public const ushort C_LOGIN_REQUEST    = 0x6100;
        public const ushort S_LOGIN_SUCCESS    = 0x6101;
        public const ushort S_LOGIN_FAILED     = 0x6102;
        public const ushort C_CHAR_LIST_REQ    = 0x6103;
        public const ushort S_CHAR_LIST        = 0x6104;
        public const ushort C_SELECT_CHAR      = 0x6105;
        public const ushort S_SELECT_CHAR_OK   = 0x6106;

        // --- Game opcode'ları (Faz 2'de genişleyecek) ---
        public const ushort C_MOVE_REQUEST     = 0x7001;
        public const ushort S_ENTITY_MOVE      = 0x7002;
        public const ushort S_ENTITY_SPAWN     = 0x7010;
        public const ushort S_ENTITY_DESPAWN   = 0x7011;
    }
}
