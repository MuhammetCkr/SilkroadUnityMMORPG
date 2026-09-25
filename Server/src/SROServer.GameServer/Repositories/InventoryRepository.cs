using Dapper;
using Microsoft.Data.SqlClient;
using SROServer.Shared.Models;

namespace SROServer.GameServer.Repositories;

/// <summary>
/// Envanter repository sözleşmesi (test edilebilirlik için arayüz).
/// </summary>
public interface IInventoryRepository
{
    Task<IEnumerable<InventorySlot>> LoadInventoryAsync(int charId);
    Task<ItemData?> GetItemAsync(long itemId);
    Task<bool> MoveItemAsync(int charId, byte fromSlot, byte toSlot);
    Task<bool> DeleteItemAsync(long itemId);
    Task<long> AddItemAsync(int charId, byte slot, int refItemId, int quantity = 1);
    Task UpdateItemQuantityAsync(long itemId, int quantity);
}

/// <summary>
/// SRO_VT_SHARD envanter (_Inventory + _Items + referans tabloları) veri erişimi (Dapper).
/// Tüm çağrılar; veritabanı yoksa çağıran katman tarafından güvenle yakalanabilir.
/// </summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private readonly string _shardConnectionString;

    public InventoryRepository(string shardConnectionString)
    {
        _shardConnectionString = shardConnectionString;
    }

    /// <summary>
    /// Bir karakterin tüm envanter slotlarını (eşya + referans verisiyle birleşik) yükler.
    /// </summary>
    public async Task<IEnumerable<InventorySlot>> LoadInventoryAsync(int charId)
    {
        const string sql = @"
            SELECT inv.Slot        AS Slot,
                   it.ID64         AS Id64,
                   it.RefItemID    AS RefItemId,
                   it.OptLevel     AS OptLevel,
                   it.MagParamNum  AS MagParamNum,
                   c.CodeName128   AS CodeName,
                   c.NameStrID128  AS Name,
                   c.TypeID1       AS TypeId1,
                   c.TypeID2       AS TypeId2,
                   c.TypeID3       AS TypeId3,
                   c.Price         AS Price,
                   c.ReqLevel1     AS RequiredLevel,
                   ri.MaxStack     AS MaxStack,
                   CAST(ri.Dur_U AS int) AS Durability,
                   it.Data         AS Quantity
            FROM _Inventory inv
            INNER JOIN _Items it       ON inv.ItemID = it.ID64
            INNER JOIN _RefObjCommon c ON it.RefItemID = c.ID
            INNER JOIN _RefObjItem ri  ON c.ID = ri.ID
            WHERE inv.CharID = @CharId
            ORDER BY inv.Slot";

        await using var connection = new SqlConnection(_shardConnectionString);
        var rows = await connection.QueryAsync(sql, new { CharId = charId });

        var slots = new List<InventorySlot>();
        foreach (var row in rows)
        {
            var item = new ItemData
            {
                Id64 = (long)row.Id64,
                RefItemId = (int)row.RefItemId,
                CodeName = (string)(row.CodeName ?? string.Empty),
                Name = (string)(row.Name ?? string.Empty),
                OptLevel = (byte)(row.OptLevel ?? (byte)0),
                MagParamNum = (byte)(row.MagParamNum ?? (byte)0),
                TypeId1 = (int)(byte)row.TypeId1,
                TypeId2 = (int)(byte)row.TypeId2,
                TypeId3 = (int)(byte)row.TypeId3,
                Price = (int)row.Price,
                RequiredLevel = (byte)row.RequiredLevel,
                MaxStack = (int)row.MaxStack,
                Durability = (int)row.Durability,
                Quantity = (int)row.Quantity
            };

            slots.Add(new InventorySlot
            {
                Slot = (byte)row.Slot,
                ItemId = item.Id64,
                Item = item
            });
        }

        return slots;
    }

    /// <summary>Tek bir eşyanın örnek verisini kimliğine göre döner (yoksa null).</summary>
    public async Task<ItemData?> GetItemAsync(long itemId)
    {
        const string sql = @"
            SELECT it.ID64        AS Id64,
                   it.RefItemID   AS RefItemId,
                   it.OptLevel    AS OptLevel,
                   it.MagParamNum AS MagParamNum,
                   c.CodeName128  AS CodeName,
                   c.NameStrID128 AS Name,
                   c.TypeID1      AS TypeId1,
                   c.TypeID2      AS TypeId2,
                   c.TypeID3      AS TypeId3,
                   c.Price        AS Price,
                   c.ReqLevel1    AS RequiredLevel,
                   ri.MaxStack    AS MaxStack,
                   CAST(ri.Dur_U AS int) AS Durability,
                   it.Data        AS Quantity
            FROM _Items it
            INNER JOIN _RefObjCommon c ON it.RefItemID = c.ID
            INNER JOIN _RefObjItem ri  ON c.ID = ri.ID
            WHERE it.ID64 = @ItemId";

        await using var connection = new SqlConnection(_shardConnectionString);
        return await connection.QueryFirstOrDefaultAsync<ItemData>(sql, new { ItemId = itemId });
    }

    /// <summary>
    /// İki slotu takas eder / eşyayı boş bir slota taşır.
    /// Hedef slot boşsa taşır, doluysa iki slotun eşyalarını değiştirir.
    /// </summary>
    public async Task<bool> MoveItemAsync(int charId, byte fromSlot, byte toSlot)
    {
        await using var connection = new SqlConnection(_shardConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            long? fromItem = await connection.QueryFirstOrDefaultAsync<long?>(
                "SELECT ItemID FROM _Inventory WHERE CharID = @CharId AND Slot = @Slot",
                new { CharId = charId, Slot = fromSlot }, tx);

            if (fromItem == null)
            {
                await tx.RollbackAsync();
                return false;
            }

            long? toItem = await connection.QueryFirstOrDefaultAsync<long?>(
                "SELECT ItemID FROM _Inventory WHERE CharID = @CharId AND Slot = @Slot",
                new { CharId = charId, Slot = toSlot }, tx);

            // Kaynağı geçici olarak boşalt (PK çakışmasını önlemek için).
            await connection.ExecuteAsync(
                "DELETE FROM _Inventory WHERE CharID = @CharId AND Slot = @Slot",
                new { CharId = charId, Slot = fromSlot }, tx);

            if (toItem != null)
            {
                // Hedefteki eşyayı kaynağa taşı (takas).
                await connection.ExecuteAsync(
                    "UPDATE _Inventory SET ItemID = @ItemId WHERE CharID = @CharId AND Slot = @Slot",
                    new { ItemId = toItem, CharId = charId, Slot = fromSlot }, tx);
                await connection.ExecuteAsync(
                    "UPDATE _Inventory SET ItemID = @ItemId WHERE CharID = @CharId AND Slot = @Slot",
                    new { ItemId = fromItem, CharId = charId, Slot = toSlot }, tx);
            }
            else
            {
                // Hedef boş: kaynağı hedefe ekle.
                await connection.ExecuteAsync(
                    "INSERT INTO _Inventory (CharID, Slot, ItemID) VALUES (@CharId, @Slot, @ItemId)",
                    new { CharId = charId, Slot = toSlot, ItemId = fromItem }, tx);
            }

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>Bir eşyayı envanterden ve _Items tablosundan tamamen siler.</summary>
    public async Task<bool> DeleteItemAsync(long itemId)
    {
        await using var connection = new SqlConnection(_shardConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM _Inventory WHERE ItemID = @ItemId",
                new { ItemId = itemId }, tx);
            int affected = await connection.ExecuteAsync(
                "DELETE FROM _Items WHERE ID64 = @ItemId",
                new { ItemId = itemId }, tx);
            await tx.CommitAsync();
            return affected > 0;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Yeni bir eşya örneği oluşturur (_Items) ve belirtilen slota yerleştirir (_Inventory).
    /// Oluşturulan eşyanın ID64 değerini döner.
    /// </summary>
    public async Task<long> AddItemAsync(int charId, byte slot, int refItemId, int quantity = 1)
    {
        const string insertItem = @"
            INSERT INTO _Items (RefItemID, OptLevel, Variance, Data, MagParamNum, Serial64)
            VALUES (@RefItemId, 0, 0, @Quantity, 0, 0);
            SELECT CAST(SCOPE_IDENTITY() AS bigint);";

        await using var connection = new SqlConnection(_shardConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            long newId = await connection.ExecuteScalarAsync<long>(
                insertItem, new { RefItemId = refItemId, Quantity = quantity }, tx);

            await connection.ExecuteAsync(
                "INSERT INTO _Inventory (CharID, Slot, ItemID) VALUES (@CharId, @Slot, @ItemId)",
                new { CharId = charId, Slot = slot, ItemId = newId }, tx);

            await tx.CommitAsync();
            return newId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>Yığılabilir bir eşyanın adedini (Data alanı) günceller.</summary>
    public async Task UpdateItemQuantityAsync(long itemId, int quantity)
    {
        const string sql = "UPDATE _Items SET Data = @Quantity WHERE ID64 = @ItemId";
        await using var connection = new SqlConnection(_shardConnectionString);
        await connection.ExecuteAsync(sql, new { Quantity = quantity, ItemId = itemId });
    }
}
