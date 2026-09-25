namespace SROServer.Shared.Constants;

/// <summary>
/// İstemci ve sunucu arasında kullanılan paket işlem kodları (opcode).
/// Adlandırma kuralı: C_ = istemciden sunucuya, S_ = sunucudan istemciye.
/// Bu sabitler hem .NET sunucusunda hem de Unity istemcisinde birebir aynıdır.
/// </summary>
public static class PacketOpcodes
{
    // --- Auth (kimlik doğrulama) opcode'ları ---
    public const ushort C_LOGIN_REQUEST    = 0x6100; // İstemci: giriş isteği
    public const ushort S_LOGIN_SUCCESS    = 0x6101; // Sunucu: giriş başarılı (token döner)
    public const ushort S_LOGIN_FAILED     = 0x6102; // Sunucu: giriş başarısız (hata kodu döner)
    public const ushort C_CHAR_LIST_REQ    = 0x6103; // İstemci: karakter listesi isteği
    public const ushort S_CHAR_LIST        = 0x6104; // Sunucu: karakter listesi
    public const ushort C_SELECT_CHAR      = 0x6105; // İstemci: karakter seçimi
    public const ushort S_SELECT_CHAR_OK   = 0x6106; // Sunucu: karakter seçimi onayı

    // --- Game (oyun) opcode'ları — Faz 2 ---

    // Hareket
    public const ushort C_MOVE_REQUEST     = 0x7001; // İstemci: hareket isteği (hedef pozisyon)
    public const ushort S_ENTITY_MOVE      = 0x7002; // Sunucu: varlık hareketi bildirimi
    public const ushort C_STOP_REQUEST     = 0x7003; // İstemci: durma isteği
    public const ushort S_ENTITY_STOP      = 0x7004; // Sunucu: varlık durdu bildirimi

    // Spawn / Despawn
    public const ushort S_ENTITY_SPAWN     = 0x7010; // Sunucu: varlık dünyaya girdi
    public const ushort S_ENTITY_DESPAWN   = 0x7011; // Sunucu: varlık dünyadan çıktı
    public const ushort S_INIT_DATA        = 0x7020; // Sunucu: oyun dünyasına giriş verisi

    // Karakter durumu
    public const ushort S_HP_UPDATE        = 0x7030; // Sunucu: HP güncelleme
    public const ushort S_MP_UPDATE        = 0x7031; // Sunucu: MP güncelleme

    // Sector (bölge)
    public const ushort C_SECTOR_CHANGE    = 0x7040; // İstemci: sektör değişimi bildirimi
    public const ushort S_SECTOR_ACK       = 0x7041; // Sunucu: sektör değişimi onayı
}
