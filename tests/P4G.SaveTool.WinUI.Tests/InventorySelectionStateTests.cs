using P4G.SaveTool.Catalog;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class InventorySelectionStateTests
{
    [Fact]
    public void DeleteSelectionSuppressesInventoryAutoSelectUntilSelectionResumes()
    {
        InventorySelectionState selectionState = new();
        IReadOnlyList<InventoryStackViewState> inventoryEntries =
        [
            new InventoryStackViewState(0, 1024, "0x3F7", (byte)ItemCategoryId.Other, "Other", 1),
        ];

        Assert.True(selectionState.ShouldAutoSelectFirstEntry(true, inventoryEntries, null, null, null));

        selectionState.DisableAutoSelectAfterDelete();

        Assert.False(selectionState.ShouldAutoSelectFirstEntry(true, inventoryEntries, null, null, null));

        selectionState.EnableAutoSelect();

        Assert.True(selectionState.ShouldAutoSelectFirstEntry(true, inventoryEntries, null, null, null));
        Assert.False(selectionState.ShouldAutoSelectFirstEntry(true, inventoryEntries, (byte)ItemCategoryId.Other, null, null));
    }

    [Theory]
    [InlineData((byte)1, 1025, true, 1025)]
    [InlineData((byte)0, 1025, false, 0)]
    public void AddUpdateSelectionTargetsEditedEntryWhenQuantityIsPositive(
        byte quantity,
        ushort selectedItemId,
        bool expectedResult,
        ushort expectedSelectedEntryId)
    {
        InventorySelectionState selectionState = new();

        bool result = InventorySelectionState.TrySelectEditedEntry(quantity, selectedItemId, out ushort selectedEntryId);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedSelectedEntryId, selectedEntryId);
    }

    [Fact]
    public void SuccessfulZeroQuantityEditClearsInventorySelection()
    {
        byte? selectedCategoryId = (byte)ItemCategoryId.Other;
        ushort? selectedItemId = 1025;
        ushort? selectedEntryId = 1025;

        InventorySelectionState.ApplySuccessfulQuantityEdit(
            0,
            ref selectedCategoryId,
            ref selectedItemId,
            ref selectedEntryId);

        Assert.Null(selectedCategoryId);
        Assert.Null(selectedItemId);
        Assert.Null(selectedEntryId);
    }

    [Fact]
    public void SuccessfulPositiveQuantityEditTargetsEditedEntry()
    {
        byte? selectedCategoryId = (byte)ItemCategoryId.Other;
        ushort? selectedItemId = 1025;
        ushort? selectedEntryId = null;

        InventorySelectionState.ApplySuccessfulQuantityEdit(
            3,
            ref selectedCategoryId,
            ref selectedItemId,
            ref selectedEntryId);

        Assert.Equal((byte)ItemCategoryId.Other, selectedCategoryId);
        Assert.Equal((ushort)1025, selectedItemId);
        Assert.Equal((ushort)1025, selectedEntryId);
    }

    [Fact]
    public void QuantityTextHydrationIsSkippedWhenInventoryContextDoesNotChange()
    {
        InventorySelectionState selectionState = new();

        Assert.True(selectionState.ShouldHydrateQuantityText((byte)ItemCategoryId.Other, 1025, 1025, "3"));
        Assert.False(selectionState.ShouldHydrateQuantityText((byte)ItemCategoryId.Other, 1025, 1025, "3"));
        Assert.True(selectionState.ShouldHydrateQuantityText((byte)ItemCategoryId.Other, 1026, 1026, "3"));
    }

    [Fact]
    public void ResetClearsAutoSelectSuppressionAndQuantityTextContext()
    {
        InventorySelectionState selectionState = new();
        IReadOnlyList<InventoryStackViewState> inventoryEntries =
        [
            new InventoryStackViewState(0, 1024, "0x3F7", (byte)ItemCategoryId.Other, "Other", 1),
        ];

        selectionState.DisableAutoSelectAfterDelete();
        Assert.False(selectionState.ShouldAutoSelectFirstEntry(true, inventoryEntries, null, null, null));

        Assert.True(selectionState.ShouldHydrateQuantityText((byte)ItemCategoryId.Other, 1025, 1025, "3"));
        Assert.False(selectionState.ShouldHydrateQuantityText((byte)ItemCategoryId.Other, 1025, 1025, "3"));

        selectionState.Reset();

        Assert.True(selectionState.ShouldAutoSelectFirstEntry(true, inventoryEntries, null, null, null));
        Assert.True(selectionState.ShouldHydrateQuantityText((byte)ItemCategoryId.Other, 1025, 1025, "3"));
    }
}
