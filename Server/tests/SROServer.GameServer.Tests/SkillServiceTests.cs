using System;
using System.Threading.Tasks;
using Moq;
using SROServer.GameServer.Cache;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.GameServer.World;
using SROServer.Shared.Models;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// SkillService iş mantığı testleri (MP yetersizliği, bekleme süresi, başarılı kullanım).
/// SkillReferenceCache test API'si ile doldurulur; kendine hedefli (TargetType=0)
/// yetenekler kullanıldığından dünya varlığı kurulumuna gerek yoktur.
/// </summary>
public sealed class SkillServiceTests
{
    private static SkillService MakeService(SkillReferenceCache cache)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.SaveVitalsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        var combat = new CombatService(repo.Object, new Random(1));
        return new SkillService(cache, combat, WorldManager.Instance);
    }

    private static SkillData MakeSkill(int id, byte reqLevel, int mpCost, int cooldown, byte targetType)
    {
        return new SkillData
        {
            Id = id,
            CodeName = $"SKILL_TEST_{id}",
            Name = $"Test Skill {id}",
            ReqLevel = reqLevel,
            MPCost = mpCost,
            Cooldown = cooldown,
            TargetType = targetType
        };
    }

    private static PlayerEntity MakePlayer(int mp)
    {
        return new PlayerEntity
        {
            UniqueId = 100,
            CharacterId = 1,
            CharName = "Caster",
            Level = 10,
            MP = mp,
            MaxMP = 200
        };
    }

    [Fact]
    public async Task UseSkill_NotEnoughMP_ReturnsNotEnoughMP()
    {
        var cache = new SkillReferenceCache();
        cache.AddSkillForTest(MakeSkill(id: 1, reqLevel: 1, mpCost: 50, cooldown: 0, targetType: 0));
        SkillService service = MakeService(cache);
        PlayerEntity player = MakePlayer(mp: 10);

        SkillUseResult result = await service.UseSkillAsync(player, skillId: 1, targetId: 0);

        Assert.Equal(SkillUseResult.NotEnoughMP, result);
    }

    [Fact]
    public async Task UseSkill_OnCooldown_ReturnsOnCooldown()
    {
        var cache = new SkillReferenceCache();
        cache.AddSkillForTest(MakeSkill(id: 2, reqLevel: 1, mpCost: 10, cooldown: 60000, targetType: 0));
        SkillService service = MakeService(cache);
        PlayerEntity player = MakePlayer(mp: 100);

        // Bekleme suresini baslat, sonra tekrar kullanmayi dene.
        service.SetCooldown(player, skillId: 2);
        SkillUseResult result = await service.UseSkillAsync(player, skillId: 2, targetId: 0);

        Assert.Equal(SkillUseResult.OnCooldown, result);
    }

    [Fact]
    public async Task UseSkill_ValidRequest_ReturnsSuccess()
    {
        var cache = new SkillReferenceCache();
        // Kendine hedefli (TargetType=0) yetenek: hedef dogrulamasi gerektirmez.
        cache.AddSkillForTest(MakeSkill(id: 3, reqLevel: 1, mpCost: 10, cooldown: 1000, targetType: 0));
        SkillService service = MakeService(cache);
        PlayerEntity player = MakePlayer(mp: 100);

        SkillUseResult result = await service.UseSkillAsync(player, skillId: 3, targetId: 0);

        Assert.Equal(SkillUseResult.Success, result);
        Assert.Equal(90, player.MP); // MP dusuldu.
    }
}
