namespace SROServer.Auth.Models;

/// <summary>
/// İstemciden gelen giriş isteğinin (C_LOGIN_REQUEST) parse edilmiş hali.
/// </summary>
public sealed class LoginPacket
{
    /// <summary>Kullanıcı adı.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Şifre (düz metin — sunucu tarafında BCrypt ile doğrulanır).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>İstemci sürümü (versiyon kontrolü için).</summary>
    public string ClientVersion { get; set; } = string.Empty;
}
