using Moq;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.GameServer.World;
using SROServer.Shared.Models;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// MovementService (hareket servisi) davranışlarını doğrulayan testler:
/// mesafe doğrulaması (anti-cheat), bölge geçişi tespiti ve throttle'lı pozisyon kaydı.
/// </summary>
public class MovementServiceTests
{
    private static MovementService CreateService(out Mock<ICharacterRepository> repoMock)
    {
        WorldManager world = WorldManager.Instance;
        var sectorManager = new SectorManager(world);
        repoMock = new Mock<ICharacterRepository>();
        return new MovementService(world, sectorManager, repoMock.Object);
    }

    private static PlayerEntity MakePlayer(SROVector3 pos, short regionId)
    {
        return new PlayerEntity
        {
            UniqueId = WorldManager.Instance.GetNextEntityId(),
            JID = 8000 + (int)(pos.X + pos.Z),
            CharName = "Gezgin",
            Position = pos,
            RegionId = regionId,
            State = EntityState.Idle,
            LastPositionSave = DateTime.MinValue
        };
    }

    [Fact]
    public async Task ProcessMoveRequest_GecerliHareketiKabulEder()
    {
        MovementService service = CreateService(out _);
        var start = new SROVector3(100f, 0f, 100f);
        PlayerEntity player = MakePlayer(start, regionId: SectorManager.ResolveRegionId(start));

        var target = new SROVector3(110f, 0f, 105f);
        MoveResult result = await service.ProcessMoveRequestAsync(player, target);

        Assert.True(result.Accepted);
        Assert.Equal(target, result.TargetPosition);
        Assert.Equal(target, player.Position);
        Assert.True(player.IsMoving);
        Assert.Equal(EntityState.Moving, player.State);
    }

    [Fact]
    public async Task ProcessMoveRequest_AsiriSicramayiReddeder()
    {
        MovementService service = CreateService(out _);
        var start = new SROVector3(100f, 0f, 100f);
        PlayerEntity player = MakePlayer(start, regionId: SectorManager.ResolveRegionId(start));

        // 250 birimlik eşiğin çok üzerinde bir ışınlanma denemesi.
        var target = new SROVector3(100000f, 0f, 100000f);
        MoveResult result = await service.ProcessMoveRequestAsync(player, target);

        Assert.False(result.Accepted);
        Assert.NotNull(result.RejectReason);
        // Reddedilen harekette oyuncunun konumu değişmemeli.
        Assert.Equal(start, player.Position);
    }

    [Fact]
    public void NeedsRegionChange_SektorSinirindaTrueDoner()
    {
        MovementService service = CreateService(out _);

        var current = new SROVector3(10f, 0f, 10f);   // Sektör (0,0)
        var target = new SROVector3(400f, 0f, 10f);   // Sektör (2,0) — 192*2 sınırının ötesi
        short currentRegion = SectorManager.ResolveRegionId(current);

        bool changed = service.NeedsRegionChange(current, target, currentRegion);

        Assert.True(changed);
    }

    [Fact]
    public void NeedsRegionChange_AyniSektordeFalseDoner()
    {
        MovementService service = CreateService(out _);

        var current = new SROVector3(10f, 0f, 10f);
        var target = new SROVector3(50f, 0f, 60f);    // Hâlâ sektör (0,0)
        short currentRegion = SectorManager.ResolveRegionId(current);

        bool changed = service.NeedsRegionChange(current, target, currentRegion);

        Assert.False(changed);
    }

    [Fact]
    public async Task ThrottleSavePosition_SureGectiyseKaydeder()
    {
        MovementService service = CreateService(out Mock<ICharacterRepository> repoMock);
        var pos = new SROVector3(100f, 0f, 100f);
        PlayerEntity player = MakePlayer(pos, regionId: SectorManager.ResolveRegionId(pos));
        player.LastPositionSave = DateTime.MinValue; // Kayıt zamanı çoktan geçmiş.

        await service.ThrottleSavePositionAsync(player);

        repoMock.Verify(
            r => r.SavePositionAsync(player.JID, It.IsAny<SROVector3>(), player.RegionId),
            Times.Once);
    }

    [Fact]
    public async Task ThrottleSavePosition_YakinZamandaKaydedildiyseAtlar()
    {
        MovementService service = CreateService(out Mock<ICharacterRepository> repoMock);
        var pos = new SROVector3(100f, 0f, 100f);
        PlayerEntity player = MakePlayer(pos, regionId: SectorManager.ResolveRegionId(pos));
        player.LastPositionSave = DateTime.UtcNow; // Az önce kaydedildi.

        await service.ThrottleSavePositionAsync(player);

        repoMock.Verify(
            r => r.SavePositionAsync(It.IsAny<int>(), It.IsAny<SROVector3>(), It.IsAny<short>()),
            Times.Never);
    }
}
