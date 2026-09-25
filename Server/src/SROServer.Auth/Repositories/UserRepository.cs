using Dapper;
using Microsoft.Data.SqlClient;
using SROServer.Auth.Models;
using SROServer.Shared.Models;

namespace SROServer.Auth.Repositories;

/// <summary>
/// Kullanıcı repository sözleşmesi. Test edilebilirlik (mock) için arayüz olarak tanımlanır.
/// </summary>
public interface IUserRepository
{
    /// <summary>Kullanıcı adına göre hesabı getirir (silinmiş hesaplar hariç).</summary>
    Task<UserEntity?> GetByUsernameAsync(string username);

    /// <summary>Son giriş tarihini günceller ve deneme sayacını sıfırlar.</summary>
    Task UpdateLastLoginAsync(int jid);

    /// <summary>Başarısız giriş deneme sayacını bir artırır.</summary>
    Task IncrementRetryCountAsync(int jid);

    /// <summary>Bir hesaba ait silinmemiş karakterleri getirir.</summary>
    Task<IEnumerable<Character>> GetCharactersAsync(int jid);
}

/// <summary>
/// Kullanıcı ve karakter verilerine erişimi sağlayan repository.
/// Dapper kullanarak SQL Server (SRO_VT_ACCOUNT ve SRO_VT_SHARD) veritabanlarına bağlanır.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly string _accountConnectionString;
    private readonly string _shardConnectionString;

    /// <summary>
    /// Repository'yi hesap ve shard veritabanı bağlantı dizeleriyle başlatır.
    /// </summary>
    /// <param name="accountConnectionString">SRO_VT_ACCOUNT bağlantı dizesi.</param>
    /// <param name="shardConnectionString">SRO_VT_SHARD bağlantı dizesi.</param>
    public UserRepository(string accountConnectionString, string shardConnectionString)
    {
        _accountConnectionString = accountConnectionString;
        _shardConnectionString = shardConnectionString;
    }

    /// <summary>
    /// Kullanıcı adına göre hesabı getirir. Silinmiş hesaplar (Status = 2) hariç tutulur.
    /// </summary>
    /// <param name="username">Aranacak kullanıcı adı.</param>
    /// <returns>Bulunursa UserEntity, aksi halde null.</returns>
    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        const string sql = @"
            SELECT JID, StrUserID, StrPasswd, Status, SilkPoint, RetryCount
            FROM TB_User
            WHERE StrUserID = @Username AND Status <> 2";

        await using var connection = new SqlConnection(_accountConnectionString);
        return await connection.QueryFirstOrDefaultAsync<UserEntity>(sql, new { Username = username });
    }

    /// <summary>
    /// Kullanıcının son giriş tarihini günceller.
    /// </summary>
    /// <param name="jid">Hesap kimliği (JID).</param>
    public async Task UpdateLastLoginAsync(int jid)
    {
        const string sql = @"
            UPDATE TB_User
            SET LoginDate = GETDATE(), RetryCount = 0
            WHERE JID = @JID";

        await using var connection = new SqlConnection(_accountConnectionString);
        await connection.ExecuteAsync(sql, new { JID = jid });
    }

    /// <summary>
    /// Başarısız giriş deneme sayacını bir artırır.
    /// </summary>
    /// <param name="jid">Hesap kimliği (JID).</param>
    public async Task IncrementRetryCountAsync(int jid)
    {
        const string sql = @"
            UPDATE TB_User
            SET RetryCount = RetryCount + 1
            WHERE JID = @JID";

        await using var connection = new SqlConnection(_accountConnectionString);
        await connection.ExecuteAsync(sql, new { JID = jid });
    }

    /// <summary>
    /// Bir hesaba ait, silinmemiş tüm karakterleri getirir (SRO_VT_SHARD._User).
    /// </summary>
    /// <param name="jid">Hesap kimliği (JID).</param>
    /// <returns>Karakter listesi.</returns>
    public async Task<IEnumerable<Character>> GetCharactersAsync(int jid)
    {
        const string sql = @"
            SELECT CharName16, CurLevel, MaxLevel, HP, MP, RemainStatPoint, PVPState
            FROM _User
            WHERE UserJID = @JID AND Deleted = 0";

        await using var connection = new SqlConnection(_shardConnectionString);
        return await connection.QueryAsync<Character>(sql, new { JID = jid });
    }
}
