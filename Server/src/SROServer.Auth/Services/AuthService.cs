using SROServer.Auth.Models;
using SROServer.Auth.Repositories;
using SROServer.Shared.Models;

namespace SROServer.Auth.Services;

/// <summary>
/// Giriş sonucunun durum kodları.
/// </summary>
public enum AuthStatus
{
    /// <summary>Giriş başarılı.</summary>
    Success,

    /// <summary>Kullanıcı adı veya şifre hatalı.</summary>
    Failed,

    /// <summary>Hesap banlı/askıda (Status = 1).</summary>
    Banned
}

/// <summary>
/// Bir giriş denemesinin sonucunu taşıyan sonuç nesnesi.
/// </summary>
public sealed class AuthResult
{
    /// <summary>Giriş durumu.</summary>
    public AuthStatus Status { get; init; }

    /// <summary>Başarılıysa oturum tokenı; aksi halde null.</summary>
    public string? Token { get; init; }

    /// <summary>Başarılıysa hesap kimliği (JID); aksi halde 0.</summary>
    public int Jid { get; init; }

    /// <summary>Kullanıcıya gösterilecek mesaj.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Başarılı bir sonuç oluşturur.</summary>
    public static AuthResult Ok(string token, int jid) => new()
    {
        Status = AuthStatus.Success,
        Token = token,
        Jid = jid,
        Message = "Giriş başarılı."
    };

    /// <summary>Başarısız (kimlik hatalı) bir sonuç oluşturur.</summary>
    public static AuthResult Fail(string message = "Kullanıcı adı veya şifre hatalı.") => new()
    {
        Status = AuthStatus.Failed,
        Message = message
    };

    /// <summary>Banlı hesap sonucu oluşturur.</summary>
    public static AuthResult BannedResult(string message = "Hesabınız askıya alınmış.") => new()
    {
        Status = AuthStatus.Banned,
        Message = message
    };
}

/// <summary>
/// Kimlik doğrulama iş mantığı. Kullanıcı doğrulama, şifre kontrolü (BCrypt)
/// ve oturum tokenı üretimini yönetir.
/// </summary>
public sealed class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly TokenService _tokenService;

    /// <summary>
    /// AuthService'i repository ve token servisiyle başlatır.
    /// </summary>
    public AuthService(IUserRepository userRepository, TokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Kullanıcı adı ve şifre ile giriş yapmayı dener.
    /// </summary>
    /// <param name="username">Kullanıcı adı.</param>
    /// <param name="password">Düz metin şifre.</param>
    /// <returns>Giriş sonucu (Success / Failed / Banned).</returns>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        // Boş girdi kontrolü.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Fail();

        // Kullanıcıyı veritabanından çek.
        UserEntity? user = await _userRepository.GetByUsernameAsync(username);
        if (user is null)
            return AuthResult.Fail();

        // Banlı/askıda hesap kontrolü (Status = 1).
        if (user.Status == 1)
            return AuthResult.BannedResult();

        // Şifre doğrulaması (BCrypt hash karşılaştırması).
        bool passwordValid = VerifyPassword(password, user.StrPasswd);
        if (!passwordValid)
        {
            // Başarısız denemede sayaç artırılır (brute-force takibi için).
            await _userRepository.IncrementRetryCountAsync(user.JID);
            return AuthResult.Fail();
        }

        // Başarılı giriş — son giriş tarihini güncelle ve token üret.
        await _userRepository.UpdateLastLoginAsync(user.JID);
        string token = _tokenService.CreateToken(user.JID);

        return AuthResult.Ok(token, user.JID);
    }

    /// <summary>
    /// Bir hesaba ait karakter listesini getirir.
    /// </summary>
    /// <param name="jid">Hesap kimliği (JID).</param>
    public async Task<IEnumerable<Character>> GetCharacterListAsync(int jid)
    {
        return await _userRepository.GetCharactersAsync(jid);
    }

    /// <summary>
    /// Düz metin şifreyi saklanan BCrypt hash ile karşılaştırır.
    /// Saklanan değer geçerli bir BCrypt hash değilse güvenli şekilde false döner.
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Saklanan değer BCrypt formatında değilse doğrulama başarısız sayılır.
            return false;
        }
    }
}
