namespace SROServer.Shared.Models;

/// <summary>
/// Envanterdeki tek bir slotu temsil eder. Boş bir slotta Item null olur.
/// Bu sınıf istemci ve sunucu arasında paylaşılır.
/// </summary>
public sealed class InventorySlot
{
    /// <summary>Slot indeksi (0 tabanlı; ekipman + çanta slotları).</summary>
    public byte Slot { get; set; }

    /// <summary>Slottaki eşyanın veritabanı kimliği (_Items.ID64). Boşsa 0.</summary>
    public long ItemId { get; set; }

    /// <summary>Slottaki eşyanın tam verisi. Boş slotta null.</summary>
    public ItemData? Item { get; set; }

    /// <summary>Slot boş mu? (Eşya referansı yoksa boştur.)</summary>
    public bool IsEmpty => Item == null;
}
