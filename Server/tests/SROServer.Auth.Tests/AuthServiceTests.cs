using Moq;
using SROServer.Auth.Models;
using SROServer.Auth.Repositories;
using SROServer.Auth.Services;
using Xunit;

namespace SROServer.Auth.Tests;

/// <summary>
/// AuthService için temel birim testleri.
/// UserRepository, Moq ile taklit edilerek (mock) veritabanı bağımlılığı ortadan kaldırılır.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repoMock;
    private readonly TokenService _tokenService;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _repoMock = new Mock<IUserRepository>();
        _tokenService = new TokenService();
        _authService = new AuthService(_repoMock.Object, _tokenService);
    }

    /// <summary>
    /// Yardımcı: verilen düz metin şifreyi BCrypt hash'e çevirir.
    /// </summary>
    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        // Arrange: geçerli, aktif bir kullanıcı ve doğru şifre.
        var user = new UserEntity
        {
            JID = 42,
            StrUserID = "testuser",
            StrPasswd = Hash("dogruSifre123"),
            Status = 0
        };
        _repoMock.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);

        // Act
        AuthResult result = await _authService.LoginAsync("testuser", "dogruSifre123");

        // Assert: başarı, token ve doğru JID beklenir.
        Assert.Equal(AuthStatus.Success, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal(42, result.Jid);
        _repoMock.Verify(r => r.UpdateLastLoginAsync(42), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsFailed()
    {
        // Arrange: kullanıcı var ama girilen şifre yanlış.
        var user = new UserEntity
        {
            JID = 7,
            StrUserID = "testuser",
            StrPasswd = Hash("gercekSifre"),
            Status = 0
        };
        _repoMock.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);

        // Act
        AuthResult result = await _authService.LoginAsync("testuser", "yanlisSifre");

        // Assert: başarısız sonuç ve deneme sayacının artırılması beklenir.
        Assert.Equal(AuthStatus.Failed, result.Status);
        Assert.Null(result.Token);
        _repoMock.Verify(r => r.IncrementRetryCountAsync(7), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_BannedUser_ReturnsBanned()
    {
        // Arrange: banlı/askıda hesap (Status = 1).
        var user = new UserEntity
        {
            JID = 99,
            StrUserID = "banneduser",
            StrPasswd = Hash("herhangiSifre"),
            Status = 1
        };
        _repoMock.Setup(r => r.GetByUsernameAsync("banneduser")).ReturnsAsync(user);

        // Act
        AuthResult result = await _authService.LoginAsync("banneduser", "herhangiSifre");

        // Assert: banlı sonuç beklenir; şifre doğru olsa bile giriş engellenir.
        Assert.Equal(AuthStatus.Banned, result.Status);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_ReturnsFailed()
    {
        // Arrange: kullanıcı bulunamıyor.
        _repoMock.Setup(r => r.GetByUsernameAsync(It.IsAny<string>()))
                 .ReturnsAsync((UserEntity?)null);

        // Act
        AuthResult result = await _authService.LoginAsync("olmayan", "sifre");

        // Assert
        Assert.Equal(AuthStatus.Failed, result.Status);
    }

    [Fact]
    public async Task LoginAsync_EmptyCredentials_ReturnsFailed()
    {
        // Act: boş kullanıcı adı/şifre.
        AuthResult result = await _authService.LoginAsync("", "");

        // Assert: repository'ye hiç gidilmeden başarısız dönmeli.
        Assert.Equal(AuthStatus.Failed, result.Status);
        _repoMock.Verify(r => r.GetByUsernameAsync(It.IsAny<string>()), Times.Never);
    }
}
