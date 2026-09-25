using SROServer.GameServer.Entities;
using SROServer.GameServer.World;
using SROServer.Shared.Models;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// WorldManager (dünya yöneticisi) ve Region (bölge) davranışlarını doğrulayan testler.
///
/// Not: WorldManager bir singleton olduğundan (Instance) durum testler arasında paylaşılır.
/// Bu nedenle her test benzersiz JID/RegionID/karakter adı kullanarak izolasyonu korur.
/// </summary>
public class WorldManagerTests
{
    private static PlayerEntity MakePlayer(int jid, short regionId, string name)
    {
        return new PlayerEntity
        {
            UniqueId = WorldManager.Instance.GetNextEntityId(),
            JID = jid,
            CharName = name,
            RegionId = regionId,
            Level = 1,
            HP = 100,
            MaxHP = 100,
            Position = SROVector3.Zero,
            State = EntityState.Idle
        };
    }

    [Fact]
    public void GetNextEntityId_ArtanBenzersizDegerlerUretir()
    {
        WorldManager world = WorldManager.Instance;

        uint first = world.GetNextEntityId();
        uint second = world.GetNextEntityId();
        uint third = world.GetNextEntityId();

        Assert.True(second > first);
        Assert.True(third > second);
    }

    [Fact]
    public void GetOrCreateRegion_AyniIdIcinAyniOrnegiDoner()
    {
        WorldManager world = WorldManager.Instance;

        Region a = world.GetOrCreateRegion(1001);
        Region b = world.GetOrCreateRegion(1001);

        Assert.Same(a, b);
        Assert.Equal((short)1001, a.RegionId);
    }

    [Fact]
    public void AddPlayer_OyuncuyuIndekseVeBolgeyeEkler()
    {
        WorldManager world = WorldManager.Instance;
        PlayerEntity player = MakePlayer(jid: 9001, regionId: 1002, name: "Kahraman");

        world.AddPlayer(player);

        Assert.Same(player, world.GetPlayer(player.UniqueId));
        Assert.Same(player, world.GetPlayerByJID(9001));

        Region region = world.GetOrCreateRegion(1002);
        Assert.True(region.Entities.ContainsKey(player.UniqueId));
        Assert.Equal(1, region.PlayerCount);
    }

    [Fact]
    public void RemovePlayer_OyuncuyuIndekstenVeBolgedenKaldirir()
    {
        WorldManager world = WorldManager.Instance;
        PlayerEntity player = MakePlayer(jid: 9002, regionId: 1003, name: "Silici");
        world.AddPlayer(player);

        world.RemovePlayer(player.UniqueId);

        Assert.Null(world.GetPlayer(player.UniqueId));
        Assert.Null(world.GetPlayerByJID(9002));

        Region region = world.GetOrCreateRegion(1003);
        Assert.False(region.Entities.ContainsKey(player.UniqueId));
    }

    [Fact]
    public void Region_EntityEnteredOlayiTetiklenir()
    {
        WorldManager world = WorldManager.Instance;
        Region region = world.GetOrCreateRegion(1004);

        EntityBase? entered = null;
        region.EntityEntered += (_, e) => entered = e;

        PlayerEntity player = MakePlayer(jid: 9003, regionId: 1004, name: "Giren");
        region.OnEntityEnter(player);

        Assert.Same(player, entered);
        Assert.True(region.Entities.ContainsKey(player.UniqueId));
    }

    [Fact]
    public void Region_EntityLeftOlayiTetiklenir()
    {
        WorldManager world = WorldManager.Instance;
        Region region = world.GetOrCreateRegion(1005);

        PlayerEntity player = MakePlayer(jid: 9004, regionId: 1005, name: "Cikan");
        region.OnEntityEnter(player);

        EntityBase? left = null;
        region.EntityLeft += (_, e) => left = e;

        region.OnEntityLeave(player);

        Assert.Same(player, left);
        Assert.False(region.Entities.ContainsKey(player.UniqueId));
    }
}
