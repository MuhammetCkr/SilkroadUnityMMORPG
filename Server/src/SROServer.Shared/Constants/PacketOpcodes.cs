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

    // --- Game (oyun) opcode'ları — Faz 2'de genişleyecek ---
    public const ushort C_MOVE_REQUEST     = 0x7001; // İstemci: hareket isteği
    public const ushort S_ENTITY_MOVE      = 0x7002; // Sunucu: varlık hareketi bildirimi
    public const ushort S_ENTITY_SPAWN     = 0x7010; // Sunucu: varlık dünyaya girdi
    public const ushort S_ENTITY_DESPAWN   = 0x7011; // Sunucu: varlık dünyadan çıktı
}
