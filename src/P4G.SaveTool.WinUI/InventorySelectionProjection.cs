using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

internal static class InventorySelectionProjection
{
    internal static IReadOnlyList<InventoryItemChoiceViewState> ResolveItemChoices(
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices,
        InventoryStackViewState? selectedEntry,
        ushort? selectedItemId,
        out InventoryItemChoiceViewState? selectedItem)
    {
        if (selectedEntry is null)
        {
            selectedItem = selectedItemId.HasValue
                ? itemChoices.FirstOrDefault(item => item.ItemId == selectedItemId.Value)
                : itemChoices.Count > 0 ? itemChoices[0] : null;
            return itemChoices;
        }

        selectedItem = itemChoices.FirstOrDefault(item => item.ItemId == selectedEntry.ItemId);
        if (selectedItem is not null)
        {
            return itemChoices;
        }

        if (selectedEntry.IsPlaceholder)
        {
            selectedItem = itemChoices.FirstOrDefault(static item => item.IsPlaceholder);
            return itemChoices;
        }

        selectedItem = new InventoryItemChoiceViewState(
            selectedEntry.ItemId,
            selectedEntry.CategoryId,
            selectedEntry.ItemName);

        List<InventoryItemChoiceViewState> projectedChoices = new(itemChoices.Count + 1);
        projectedChoices.AddRange(itemChoices);
        projectedChoices.Add(selectedItem);
        return projectedChoices;
    }

    internal static IReadOnlyList<InventoryItemChoiceViewState> ResolveEquipmentChoices(
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices,
        ushort selectedItemId,
        out InventoryItemChoiceViewState? selectedItem)
    {
        selectedItem = itemChoices.FirstOrDefault(item => item.ItemId == selectedItemId);
        if (selectedItem is not null)
        {
            return itemChoices;
        }

        if (selectedItemId == 0)
        {
            selectedItem = itemChoices.FirstOrDefault(static item => item.IsPlaceholder);
            if (selectedItem is not null)
            {
                return itemChoices;
            }
        }

        selectedItem = new InventoryItemChoiceViewState(
            selectedItemId,
            itemChoices.Count > 0 ? itemChoices[0].CategoryId : default,
            $"Unknown item ({selectedItemId})");

        List<InventoryItemChoiceViewState> projectedChoices = new(itemChoices.Count + 1);
        projectedChoices.AddRange(itemChoices);
        projectedChoices.Add(selectedItem);
        return projectedChoices;
    }
}
