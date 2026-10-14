using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Serilog;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Cache;

/// <summary>
/// Yetenek (skill) referans verilerini bellekte tutan iş parçacığı güvenli singleton
/// önbellek. Sunucu açılışında _RefSkill (+ _RefSkillMastery) tablolarından bir kez
/// yüklenir ve savaş/yetenek servisleri tarafından paylaşılır.
/// Veritabanı erişilemezse önbellek boş kalır ve sunucu yine de başlar (geliştirme modu).
/// </summary>
public sealed class SkillReferenceCache
{
    private static readonly Lazy<SkillReferenceCache> _instance =
        new(() => new SkillReferenceCache());

    /// <summary>Tekil (singleton) örnek.</summary>
    public static SkillReferenceCache Instance => _instance.Value;

    private readonly ConcurrentDictionary<int, SkillData> _skills = new();

    // MasteryID -> ustalık (mastery) görünen adı.
    private readonly ConcurrentDictionary<int, string> _masteries = new();

    /// <summary>Önbellekteki yetenek sayısı.</summary>
    public int SkillCount => _skills.Count;

    /// <summary>Önbellekteki ustalık sayısı.</summary>
    public int MasteryCount => _masteries.Count;

    // Test ve elle kullanım için örneklenebilir.
    public SkillReferenceCache() { }

    /// <summary>
    /// _RefSkill ve _RefSkillMastery tablolarını belleğe yükler.
    /// Yalnızca aktif (Service=1) yetenekler alınır.
    /// Veritabanı erişilemezse hata loglanır ve önbellek boş kalır.
    /// </summary>
    public async Task LoadAsync(IDbConnection connection)
    {
        try
        {
            // Not: Gerçek SRO şemasında _RefSkill sütunları şunlardır:
            //   Basic_Code (kod adı), Basic_Name (ad), Basic_Level (gereken seviye),
            //   Consume_MP (MP maliyeti), Action_CoolTime (bekleme süresi, ms),
            //   ReqCommon_Mastery1 (ustalık), Param1 (temel hasar/çarpan).
            const string skillSql = @"
                SELECT ID                 AS Id,
                       Basic_Code         AS CodeName,
                       Basic_Name         AS Name,
                       ReqCommon_Mastery1 AS MasteryId,
                       Basic_Level        AS ReqLevel,
                       Consume_MP         AS MPCost,
                       Action_CoolTime    AS Cooldown,
                       Param1             AS Damage
                FROM _RefSkill
                WHERE Service = 1";

            var skills = await connection.QueryAsync<SkillData>(skillSql);
            foreach (SkillData skill in skills)
                _skills[skill.Id] = skill;

            const string masterySql = @"
                SELECT ID   AS Id,
                       Code AS Code
                FROM _RefSkillMastery";

            var masteries = await connection.QueryAsync(masterySql);
            foreach (var row in masteries)
                _masteries[(int)row.Id] = (string)(row.Code ?? string.Empty);

            Log.Information("SkillReferenceCache yüklendi: {Skills} yetenek, {Mastery} ustalık.",
                _skills.Count, _masteries.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SkillReferenceCache yüklenemedi (veritabanı yok olabilir). Önbellek boş.");
        }
    }

    /// <summary>Kimliğe göre yetenek referansı döner (yoksa null).</summary>
    public SkillData? GetSkill(int skillId)
    {
        _skills.TryGetValue(skillId, out SkillData? skill);
        return skill;
    }

    /// <summary>Kimliğe göre ustalık adı döner (yoksa boş string).</summary>
    public string GetMasteryName(int masteryId)
        => _masteries.TryGetValue(masteryId, out string? name) ? name : string.Empty;

    /// <summary>Test amaçlı: önbelleğe elle yetenek ekler.</summary>
    public void AddSkillForTest(SkillData skill) => _skills[skill.Id] = skill;

    /// <summary>Test amaçlı: önbelleği temizler.</summary>
    public void ClearForTest()
    {
        _skills.Clear();
        _masteries.Clear();
    }
}
