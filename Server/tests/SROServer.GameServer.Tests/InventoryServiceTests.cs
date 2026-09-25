using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using SROServer.GameServer.Cache;
using SROServer.GameServer.Entities;
using SROServer.GameServer.Repositories;
using SROServer.GameServer.Services;
using SROServer.Shared.Models;
using Xunit;

namespace SROServer.GameServer.Tests;

/// <summary>
/// InventoryService iş mantığı testleri (taşıma, kullanma, düşürme, ağırlık).
/// Repository sahte (mock) olduğundan veritabanı gerekmez; işlemler bellek içi
/// PlayerEntity.Inventory üzerinde doğrulanır.
/// </summary>
public sealed class InventoryServiceTests
{
    private static InventoryService CreateService(out Mock<IInventoryRepository> repoMock)
    {
        repoMock = new Mock<IInventoryRepository>();
        repoMock.Setup(r => r.MoveItemAsync(It.IsAny<int>(), It.IsAny<byte>(), It.IsAny<byte>()))
            .ReturnsAsync(true);
        repoMock.Setup(r => r.DeleteItemAsync(It.IsAny<long>())).ReturnsAsync(true);
        repoMock.Setup(r => r.UpdateItemQuantityAsync(It.IsAny<long>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var cache = new ItemReferenceCache();
        return new InventoryService(repoMock.Object, cache);
    }

    private static InventorySlot MakeSlot(byte slot, long id, string codeName = "ITEM_TEST",
        int weight = 10, int quantity = 1, int price = 100)
    {
        return new InventorySlot
        {
            Slot = slot,
            ItemId = id,
            Item = new ItemData
            {
                Id64 = id,
                RefItemId = 1000,
                CodeName = codeName,
                Name = codeName,
                Weight = weight,
                Quantity = quantity,
                Price = price
            }
        };
    }

    private static PlayerEntity MakePlayer(params InventorySlot[] slots)
    {
        var player = new PlayerEntity
        {
            CharacterId = 1,
            HP = 50,
            MaxHP = 100,
            MP = 50,
            MaxMP = 100,
            MaxWeight = 10000
        };
        player.Inventory = new List<InventorySlot>(slots);
        return player;
    }

    [Fact]
    public async Task MoveItem_ValidSlots_ReturnsSuccess()
    {
        InventoryService service = CreateService(out _);
        PlayerEntity player = MakePlayer(MakeSlot(13, 500));

        ItemMoveResult result = await service.MoveItemAsync(player, 13, 20);

        Assert.Equal(ItemMoveResult.Success, result);
        Assert.Contains(player.Inventory, s => s.Slot == 20 && s.ItemId == 500);
    }

    [Fact]
    public async Task MoveItem_SameSlot_ReturnsInvalidSlot()
    {
        InventoryService service = CreateService(out _);
        PlayerEntity player = MakePlayer(MakeSlot(13, 500));

        ItemMoveResult result = await service.MoveItemAsync(player, 13, 13);

        Assert.Equal(ItemMoveResult.InvalidSlot, result);
    }

    [Fact]
    public async Task MoveItem_OccupiedTarget_ReturnsSlotOccupied()
    {
        InventoryService service = CreateService(out _);
        PlayerEntity player = MakePlayer(MakeSlot(13, 500), MakeSlot(20, 501));

        ItemMoveResult result = await service.MoveItemAsync(player, 13, 20);

        Assert.Equal(ItemMoveResult.SlotOccupied, result);
    }

    [Fact]
    public async Task UseItem_Potion_RestoresHP()
    {
        InventoryService service = CreateService(out _);
        PlayerEntity player = MakePlayer(
            MakeSlot(13, 500, codeName: "ITEM_ETC_HP_POTION_01", quantity: 5));
        int hpBefore = player.HP;

        UseResult result = await service.UseItemAsync(player, 13);

        Assert.Equal(UseResult.Success, result);
        Assert.True(player.HP > hpBefore);
        // Adet bir azalmalı.
        Assert.Equal(4, player.Inventory[0].Item!.Quantity);
    }

    [Fact]
    public async Task DropItem_EmptySlot_ReturnsSlotEmpty()
    {
        InventoryService service = CreateService(out _);
        PlayerEntity player = MakePlayer(MakeSlot(13, 500));

        DropResult result = await service.DropItemAsync(player, 99);

        Assert.Equal(DropResult.SlotEmpty, result);
    }

    [Fact]
    public void CalculateWeight_MultipleItems_ReturnsCorrectTotal()
    {
        InventoryService service = CreateService(out _);
        var slots = new List<InventorySlot>
        {
            MakeSlot(13, 500, weight: 10, quantity: 2), // 20
            MakeSlot(14, 501, weight: 25, quantity: 1), // 25
            MakeSlot(15, 502, weight: 5, quantity: 3)   // 15
        };

        int total = service.CalculateTotalWeight(slots);

        Assert.Equal(60, total);
    }
}
