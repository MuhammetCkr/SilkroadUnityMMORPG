using Dapper;
using Microsoft.Data.SqlClient;
using SROServer.GameServer.Models;

namespace SROServer.GameServer.Repositories;

/// <summary>
/// Spawn repository sözleşmesi (test edilebilirlik için arayüz).
/// </summary>
public interface ISpawnRepository
{
    Task<IEnumerable<SpawnPoint>> GetSpawnPointsByRegionAsync(short regionId);
    Task<MonsterReference?> GetMonsterReferenceAsync(int refId);
}

/// <summary>
/// SRO_VT_SHARD spawn tablolarından (Tab_RefNest, Tab_RefTactics, _RefObjCommon)
/// canavar üretim verisini okur (Dapper).
/// </summary>
public sealed class SpawnRepository : ISpawnRepository
{
    private readonly string _shardConnectionString;

    public SpawnRepository(string shardConnectionString)
    {
        _shardConnectionString = shardConnectionString;
    }

    /// <summary>
    /// Belirli bir bölgedeki tüm spawn (yuva) noktalarını, taktiğe bağlı mob referansıyla
    /// birlikte getirir. Tab_RefNest -> Tab_RefTactics ilişkisi üzerinden mob ID çözülür.
    /// </summary>
    public async Task<IEnumerable<SpawnPoint>> GetSpawnPointsByRegionAsync(short regionId)
    {
        const string sql = @"
            SELECT n.dwNestID     AS NestID,
                   n.nRegionDBID  AS RegionID,
                   n.fLocalPosX   AS LocalPosX,
                   n.fLocalPosY   AS LocalPosY,
                   n.fLocalPosZ   AS LocalPosZ,
                   n.dwTacticsID  AS TacticsID,
                   t.nObjID       AS RefMonsterID
            FROM Tab_RefNest n
            LEFT JOIN Tab_RefTactics t ON n.dwTacticsID = t.dwTacticsID
            WHERE n.nRegionDBID = @RegionId";

        await using var connection = new SqlConnection(_shardConnectionString);
        return await connection.QueryAsync<SpawnPoint>(sql, new { RegionId = regionId });
    }

    /// <summary>
    /// Bir mob referans kimliğine göre şablon verisini (_RefObjCommon) getirir.
    /// </summary>
    public async Task<MonsterReference?> GetMonsterReferenceAsync(int refId)
    {
        const string sql = @"
            SELECT ID,
                   CodeName128  AS CodeName,
                   NameStrID128 AS NameStr,
                   MaxHP        AS HP,
                   Lvl          AS Level
            FROM _RefObjCommon
            WHERE ID = @RefId";

        await using var connection = new SqlConnection(_shardConnectionString);
        return await connection.QueryFirstOrDefaultAsync<MonsterReference>(
            sql, new { RefId = refId });
    }
}
