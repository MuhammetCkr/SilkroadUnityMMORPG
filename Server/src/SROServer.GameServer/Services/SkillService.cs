using Serilog;
using SROServer.GameServer.Cache;
using SROServer.GameServer.Entities;
using SROServer.GameServer.World;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Services;

/// <summary>Yetenek (skill) kullanım sonucu.</summary>
public enum SkillUseResult
{
    Success,
    NotEnoughMP,
    LevelTooLow,
    OnCooldown,
    InvalidTarget,
    UnknownSkill
}

/// <summary>
/// Yetenek iş mantığı: kullanım koşullarının (seviye, MP, bekleme süresi) doğrulanması,
/// bekleme süresi (cooldown) yönetimi ve hasar uygulanması için CombatService ile entegrasyon.
/// Yetenek referans verisi SkillReferenceCache'ten gelir.
/// </summary>
public sealed class SkillService
{
    private readonly SkillReferenceCache _cache;
    private readonly CombatService _combatService;
    private readonly WorldManager _world;

    public SkillService(SkillReferenceCache cache, CombatService combatService, WorldManager world)
    {
        _cache = cache;
        _combatService = combatService;
        _world = world;
    }

    /// <summary>
    /// Oyuncunun belirtilen yeteneği hedefe karşı kullanmasını işler.
    /// Koşullar (bilinen yetenek, seviye, MP, bekleme süresi) sağlanırsa MP düşülür,
    /// bekleme süresi başlatılır ve (hedef varsa) hasar uygulanır.
    /// </summary>
    public async Task<SkillUseResult> UseSkillAsync(PlayerEntity player, int skillId, uint targetId)
    {
        SkillData? skill = _cache.GetSkill(skillId);
        if (skill == null)
            return SkillUseResult.UnknownSkill;

        // Seviye kontrolü.
        if (player.Level < skill.ReqLevel)
            return SkillUseResult.LevelTooLow;

        // Bekleme süresi kontrolü.
        if (IsOnCooldown(player, skillId))
            return SkillUseResult.OnCooldown;

        // MP kontrolü.
        if (player.MP < skill.MPCost)
            return SkillUseResult.NotEnoughMP;

        // Hedef gerektiren (düşman) yetenekler için hedef doğrulaması.
        EntityBase? target = null;
        if (skill.TargetType == 1 || skill.TargetType == 3)
        {
            target = _world.GetEntity(targetId);
            if (target == null || target.State == EntityState.Dead)
                return SkillUseResult.InvalidTarget;
        }

        // Koşullar sağlandı: MP düş ve bekleme süresini başlat.
        player.MP -= skill.MPCost;
        SetCooldown(player, skillId);

        // Hedef varsa hasarı uygula.
        if (target != null)
        {
            CombatResult result = await _combatService.ProcessAttackAsync(player, target, skillId);
            Log.Debug("Yetenek kullanıldı: {Char} skill={Skill} hedef={Target} hasar={Dmg}",
                player.CharName, skillId, targetId, result.Damage);
        }

        return SkillUseResult.Success;
    }

    /// <summary>Belirtilen yetenek şu anda bekleme süresinde mi?</summary>
    public bool IsOnCooldown(PlayerEntity player, int skillId)
    {
        if (player.SkillCooldowns.TryGetValue(skillId, out DateTime readyAt))
            return DateTime.UtcNow < readyAt;
        return false;
    }

    /// <summary>
    /// Yeteneğin bekleme süresini başlatır. Süre, yeteneğin referans CoolTime
    /// değerinden (ms) alınır.
    /// </summary>
    public void SetCooldown(PlayerEntity player, int skillId)
    {
        SkillData? skill = _cache.GetSkill(skillId);
        int cooldownMs = skill?.Cooldown ?? 0;
        player.SkillCooldowns[skillId] = DateTime.UtcNow.AddMilliseconds(cooldownMs);
    }
}
