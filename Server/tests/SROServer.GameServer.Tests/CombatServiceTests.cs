using System;
using System.Threading.Tasks;
using Moq;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.Shared.Models;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// CombatService iş mantığı testleri (hasar hesaplama, savunma azaltma, XP/level-up).
/// RNG belirlenimci olması için sabit tohumlu (seeded) Random enjekte edilir;
/// repository sahte (mock) olduğundan veritabanı gerekmez.
/// </summary>
public sealed class CombatServiceTests
{
    private static Mock<ICharacterRepository> MakeRepo()
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.SaveVitalsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static PlayerEntity MakeAttacker()
    {
        return new PlayerEntity
        {
            UniqueId = 1,
            CharacterId = 1,
            CharName = "Attacker",
            Level = 1,
            STR = 20,
            INT = 20,
            Luck = 0,
            HP = 100,
            MaxHP = 100
        };
    }

    private static MonsterEntity MakeMonster(int defense = 0, int hp = 1000, long expReward = 50)
    {
        return new MonsterEntity
        {
            UniqueId = 2,
            CodeName = "MOB_TEST",
            Defense = defense,
            HP = hp,
            MaxHP = hp,
            ExpReward = expReward
        };
    }

    [Fact]
    public void CalculateAttack_BasicAttack_DamageAboveZero()
    {
        // seed=1 -> ilk NextDouble=0.2487 (kritik yok, deterministik).
        var service = new CombatService(MakeRepo().Object, new Random(1));
        PlayerEntity attacker = MakeAttacker();
        MonsterEntity target = MakeMonster(defense: 0);

        CombatResult result = service.CalculateAttack(attacker, target);

        Assert.True(result.Damage > 0);
        Assert.Equal(attacker.UniqueId, result.AttackerId);
        Assert.Equal(target.UniqueId, result.TargetId);
    }

    [Fact]
    public void CalculateAttack_HighDefense_ReducedDamage()
    {
        // Aynı tohum iki serviste de aynı kritik sonucunu verir; karşılaştırma adil olur.
        var lowDefService = new CombatService(MakeRepo().Object, new Random(1));
        var highDefService = new CombatService(MakeRepo().Object, new Random(1));

        PlayerEntity attacker = MakeAttacker();
        MonsterEntity lowDef = MakeMonster(defense: 0);
        MonsterEntity highDef = MakeMonster(defense: 900);

        int lowDefDamage = lowDefService.CalculateAttack(attacker, lowDef).Damage;
        int highDefDamage = highDefService.CalculateAttack(attacker, highDef).Damage;

        Assert.True(highDefDamage < lowDefDamage,
            $"Yüksek savunma hasarı ({highDefDamage}) düşük savunma hasarından ({lowDefDamage}) küçük olmalı.");
    }

    [Fact]
    public async Task ProcessXP_KillMob_AddsExpToPlayer()
    {
        var service = new CombatService(MakeRepo().Object, new Random(1));
        PlayerEntity player = MakeAttacker();
        player.CurrentExp = 0;
        player.NextLevelExp = 100000; // Seviye atlamayı engelle.
        MonsterEntity killed = MakeMonster(expReward: 50);

        await service.ProcessXPAsync(player, killed);

        Assert.Equal(50, player.CurrentExp);
        Assert.Equal((byte)1, player.Level);
    }

    [Fact]
    public async Task ProcessXP_LevelUp_IncreasesLevel()
    {
        var repo = MakeRepo();
        var service = new CombatService(repo.Object, new Random(1));
        PlayerEntity player = MakeAttacker();
        player.Level = 1;
        player.CurrentExp = 0;
        player.NextLevelExp = 100;
        int maxHpBefore = player.MaxHP;
        MonsterEntity killed = MakeMonster(expReward: 150);

        await service.ProcessXPAsync(player, killed);

        Assert.Equal((byte)2, player.Level);
        Assert.Equal(50, player.CurrentExp);      // 150 - 100
        Assert.True(player.MaxHP > maxHpBefore);   // Seviye başına HP artışı.
        repo.Verify(r => r.SaveVitalsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Once);
    }
}
