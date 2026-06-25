using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

internal sealed class InventorySelectionState
{
    private bool suppressAutoSelectAfterDelete;
    private InventoryQuantityTextContext? quantityTextContext;

    internal bool ShouldAutoSelectFirstEntry(
        bool hasSave,
        IReadOnlyList<InventoryStackViewState> inventoryEntries,
        byte? selectedCategoryId,
        ushort? selectedItemId,
        ushort? selectedEntryId) =>
        !suppressAutoSelectAfterDelete &&
        selectedCategoryId is null &&
        selectedItemId is null &&
        selectedEntryId is null &&
        hasSave &&
        inventoryEntries.Count > 0;

    internal void EnableAutoSelect() =>
        suppressAutoSelectAfterDelete = false;

    internal void DisableAutoSelectAfterDelete() =>
        suppressAutoSelectAfterDelete = true;

    internal void Reset()
    {
        suppressAutoSelectAfterDelete = false;
        quantityTextContext = null;
    }

    internal static bool TrySelectEditedEntry(byte quantity, ushort? selectedItemId, out ushort selectedEntryId)
    {
        if (quantity > 0 && selectedItemId.HasValue)
        {
            selectedEntryId = selectedItemId.Value;
            return true;
        }

        selectedEntryId = default;
        return false;
    }

    internal static void ApplySuccessfulQuantityEdit(
        byte quantity,
        ref byte? selectedCategoryId,
        ref ushort? selectedItemId,
        ref ushort? selectedEntryId)
    {
        if (quantity == 0)
        {
            selectedCategoryId = null;
            selectedItemId = null;
            selectedEntryId = null;
            return;
        }

        if (TrySelectEditedEntry(quantity, selectedItemId, out ushort selectedEditedEntryId))
        {
            selectedEntryId = selectedEditedEntryId;
        }
    }

    internal bool ShouldHydrateQuantityText(
        byte? selectedCategoryId,
        ushort? selectedItemId,
        ushort? selectedEntryId,
        string quantityText)
    {
        InventoryQuantityTextContext currentContext =
            new(selectedCategoryId, selectedItemId, selectedEntryId, quantityText);
        if (quantityTextContext.HasValue && quantityTextContext.Value.Equals(currentContext))
        {
            return false;
        }

        quantityTextContext = currentContext;
        return true;
    }

    private readonly record struct InventoryQuantityTextContext(
        byte? SelectedCategoryId,
        ushort? SelectedItemId,
        ushort? SelectedEntryId,
        string QuantityText);
}
