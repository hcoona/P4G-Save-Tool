namespace P4G.SaveTool.Presentation;

public sealed class InventoryStackViewState
{
    public InventoryStackViewState(
        int slotIndex,
        ushort itemId,
        string itemName,
        byte categoryId,
        string categoryName,
        byte quantity,
        bool isPlaceholder = false)
    {
        SlotIndex = slotIndex;
        ItemId = itemId;
        ItemName = itemName;
        CategoryId = categoryId;
        CategoryName = categoryName;
        Quantity = quantity;
        IsPlaceholder = isPlaceholder;
    }

    public int SlotIndex { get; }

    public ushort ItemId { get; }

    public string ItemName { get; }

    public byte CategoryId { get; }

    public string CategoryName { get; }

    public byte Quantity { get; }

    public bool IsPlaceholder { get; }

    public string DisplayName => $"{ItemName} [{CategoryName}]";
}
