using Serilog;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Services;

/// <summary>
/// Savaş iş mantığı: hasar hesaplama, saldırı işleme, ölüm/XP yönetimi ve basit
/// canavar yapay zekâsı (AI) saldırısı. Hasar formülü Silkroad'dan esinlenir.
/// Deneyim kalıcılığı için CharacterRepository kullanılır (veritabanı yoksa yutulur).
/// </summary>
public sealed class CombatService
{
    private readonly ICharacterRepository _characterRepository;
    private readonly Random _random;

    // Temel kritik vuruş şansı (%5).
    private const double BaseCriticalChance = 0.05;

    // Kritik vuruş hasar çarpanı.
    private const double CriticalMultiplier = 1.5;

    public CombatService(ICharacterRepository characterRepository, Random? random = null)
    {
        _characterRepository = characterRepository;
        // Testlerde belirlenimci (deterministic) davranış için RNG dışarıdan verilebilir.
        _random = random ?? new Random();
    }

    /// <summary>
    /// İki varlık arasındaki saldırının hasarını hesaplar (kalıcılık/HP değişikliği yapmaz).
    ///
    /// SRO hasar formülü:
    ///   TemelHasar = (STR * 1.5 + INT * 0.5) * yetenekÇarpanı
    ///   NihaiHasar = TemelHasar * (1 - hedefSavunması / 1000)
    ///   Kritik şansı = %5 + (saldıranın Luck değeri * 0.1)
    /// </summary>
    public CombatResult CalculateAttack(EntityBase attacker, EntityBase target, SkillData? skill = null)
    {
        double baseDamage = GetBaseAttack(attacker);

        // Yetenek çarpanı: Damage parametresi yüzde kabul edilir (100 = 1.0x).
        double skillMultiplier = (skill != null && skill.Damage > 0)
            ? skill.Damage / 100.0
            : 1.0;
        baseDamage *= skillMultiplier;

        // Savunma azaltma (en fazla %90'a kadar sınırlanır).
        int targetDef = GetDefense(target);
        double reduction = Math.Min(0.9, targetDef / 1000.0);
        double finalDamage = baseDamage * (1.0 - reduction);

        // Kritik vuruş kontrolü.
        double critChance = BaseCriticalChance + GetLuck(attacker) * 0.001;
        bool isCritical = _random.NextDouble() < critChance;
        if (isCritical)
            finalDamage *= CriticalMultiplier;

        int damage = Math.Max(1, (int)Math.Round(finalDamage));

        int targetHp = GetHp(target);
        bool isDead = damage >= targetHp;

        return new CombatResult
        {
            AttackerId = attacker.UniqueId,
            TargetId = target.UniqueId,
            Damage = damage,
            IsCritical = isCritical,
            IsTargetDead = isDead
        };
    }

    /// <summary>
    /// Bir oyuncunun hedefe saldırısını işler: hasarı hesaplar, uygular, ölümü kontrol
    /// eder ve hedef bir canavar ise XP verir. Sonuç CombatResult olarak döner.
    /// </summary>
    public async Task<CombatResult> ProcessAttackAsync(PlayerEntity attacker, EntityBase target, int? skillId = null)
    {
        CombatResult result = CalculateAttack(attacker, target);

        ApplyDamage(target, result.Damage);
        attacker.IsInCombat = true;
        attacker.LastAttackTime = DateTime.UtcNow;

        if (result.IsTargetDead)
        {
            target.State = EntityState.Dead;
            if (target is MonsterEntity monster)
            {
                monster.AIState = MonsterState.Dead;
                await ProcessXPAsync(attacker, monster);
            }
        }

        return result;
    }

    /// <summary>
    /// Basit canavar AI saldırısı: canavarın bir oyuncuya verdiği hasarı hesaplar ve uygular.
    /// </summary>
    public CombatResult ProcessMonsterAttack(MonsterEntity monster, PlayerEntity target)
    {
        CombatResult result = CalculateAttack(monster, target);
        ApplyDamage(target, result.Damage);
        target.IsInCombat = true;

        if (result.IsTargetDead)
            target.State = EntityState.Dead;

        return result;
    }

    /// <summary>
    /// Öldürülen canavardan oyuncuya XP verir ve gerekirse seviye atlatır (level-up).
    /// Seviye atlandığında HP/MP yenilenir ve bir sonraki seviye eşiği yükseltilir.
    /// </summary>
    public async Task ProcessXPAsync(PlayerEntity player, MonsterEntity killed)
    {
        player.CurrentExp += killed.ExpReward;

        bool leveledUp = false;
        while (player.CurrentExp >= player.NextLevelExp && player.Level < byte.MaxValue)
        {
            player.CurrentExp -= player.NextLevelExp;
            player.Level += 1;
            leveledUp = true;

            // Seviye başına stat/HP artışı (basit model).
            player.MaxHP += 50;
            player.MaxMP += 20;
            player.HP = player.MaxHP;
            player.MP = player.MaxMP;

            // Bir sonraki seviye eşiği: %25 artış.
            player.NextLevelExp = (long)(player.NextLevelExp * 1.25);
        }

        if (leveledUp)
        {
            try
            {
                await _characterRepository.SaveVitalsAsync(player.CharacterId, player.HP, player.MP);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Seviye atlama kalıcı hale getirilemedi (Char={Char}).", player.CharName);
            }

            Log.Debug("Oyuncu seviye atladı: {Char} -> Lv.{Level}", player.CharName, player.Level);
        }
    }

    // ==================== Yardımcılar ====================

    /// <summary>Saldıranın temel saldırı değerini döner (oyuncu stat'tan, canavar güçten).</summary>
    private static double GetBaseAttack(EntityBase attacker) => attacker switch
    {
        PlayerEntity p => p.STR * 1.5 + p.INT * 0.5,
        MonsterEntity m => m.AttackPower,
        _ => 1
    };

    private static int GetDefense(EntityBase target) => target switch
    {
        PlayerEntity p => p.Defense,
        MonsterEntity m => m.Defense,
        _ => 0
    };

    private static int GetLuck(EntityBase attacker) => attacker switch
    {
        PlayerEntity p => p.Luck,
        _ => 0
    };

    private static int GetHp(EntityBase entity) => entity switch
    {
        PlayerEntity p => p.HP,
        MonsterEntity m => m.HP,
        _ => 0
    };

    private static void ApplyDamage(EntityBase target, int damage)
    {
        switch (target)
        {
            case PlayerEntity p:
                p.HP = Math.Max(0, p.HP - damage);
                break;
            case MonsterEntity m:
                m.HP = Math.Max(0, m.HP - damage);
                break;
        }
    }
}
