namespace P4G.SaveTool.Presentation;

public sealed record InventoryItemChoiceViewState(ushort ItemId, byte CategoryId, string Name, bool IsPlaceholder = false);
