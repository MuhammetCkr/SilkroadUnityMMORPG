using Dapper;
using Microsoft.Data.SqlClient;
using SROServer.GameServer.Models;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Repositories;

/// <summary>
/// Karakter repository sözleşmesi (test edilebilirlik için arayüz).
/// </summary>
public interface ICharacterRepository
{
    Task<CharacterData?> LoadCharacterAsync(int jid, string charName);
    Task SavePositionAsync(int jid, SROVector3 pos, short regionId);
    Task SaveVitalsAsync(int jid, int hp, int mp);
}

/// <summary>
/// SRO_VT_SHARD._User tablosuna karakter verisi okuma/yazma işlemleri (Dapper).
/// </summary>
public sealed class CharacterRepository : ICharacterRepository
{
    private readonly string _shardConnectionString;

    public CharacterRepository(string shardConnectionString)
    {
        _shardConnectionString = shardConnectionString;
    }

    /// <summary>
    /// Bir hesabın belirtilen adlı karakterini pozisyon ve canlılık dahil yükler.
    /// Silinmiş karakterler hariç tutulur.
    /// </summary>
    public async Task<CharacterData?> LoadCharacterAsync(int jid, string charName)
    {
        const string sql = @"
            SELECT CharID, UserJID, CharName16, CurLevel,
                   HP, MaxHP, MP, MaxMP,
                   CurPosX, CurPosY, CurPosZ, LatestRegion AS CurSect
            FROM _User
            WHERE UserJID = @JID AND CharName16 = @CharName AND Deleted = 0";

        await using var connection = new SqlConnection(_shardConnectionString);
        return await connection.QueryFirstOrDefaultAsync<CharacterData>(
            sql, new { JID = jid, CharName = charName });
    }

    /// <summary>
    /// Karakterin pozisyonunu ve bölgesini kaydeder.
    /// Not: Çağıran taraf throttle (5sn) uygular; bu metot doğrudan yazar.
    /// </summary>
    public async Task SavePositionAsync(int jid, SROVector3 pos, short regionId)
    {
        const string sql = @"
            UPDATE _User
            SET CurPosX = @X, CurPosY = @Y, CurPosZ = @Z, LatestRegion = @Region
            WHERE UserJID = @JID";

        await using var connection = new SqlConnection(_shardConnectionString);
        await connection.ExecuteAsync(sql, new
        {
            JID = jid,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Region = regionId
        });
    }

    /// <summary>
    /// Karakterin HP/MP değerlerini kaydeder.
    /// </summary>
    public async Task SaveVitalsAsync(int jid, int hp, int mp)
    {
        const string sql = @"
            UPDATE _User
            SET HP = @HP, MP = @MP
            WHERE UserJID = @JID";

        await using var connection = new SqlConnection(_shardConnectionString);
        await connection.ExecuteAsync(sql, new { JID = jid, HP = hp, MP = mp });
    }
}
