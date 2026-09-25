namespace SROServer.Auth.Models;

/// <summary>
/// Hesap kullanıcı modeli. SRO_VT_ACCOUNT veritabanındaki TB_User tablosundan map edilir.
/// Kimlik doğrulama işlemlerinde kullanılır.
/// </summary>
public sealed class UserEntity
{
    /// <summary>Benzersiz hesap kimliği (TB_User.JID). Birincil anahtar.</summary>
    public int JID { get; set; }

    /// <summary>Kullanıcı adı (TB_User.StrUserID).</summary>
    public string StrUserID { get; set; } = string.Empty;

    /// <summary>Şifre (TB_User.StrPasswd). BCrypt hash olarak saklanması beklenir.</summary>
    public string StrPasswd { get; set; } = string.Empty;

    /// <summary>
    /// Hesap durumu (TB_User.Status).
    /// 0 = aktif, 1 = banlı/askıda, 2 = silinmiş (giriş engellenir).
    /// </summary>
    public byte Status { get; set; }

    /// <summary>Silk puanı bakiyesi (TB_User.SilkPoint).</summary>
    public int SilkPoint { get; set; }

    /// <summary>Başarısız giriş deneme sayısı (TB_User.RetryCount).</summary>
    public int RetryCount { get; set; }
}
