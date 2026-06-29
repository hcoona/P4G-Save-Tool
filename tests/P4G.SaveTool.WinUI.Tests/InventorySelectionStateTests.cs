using P4G.SaveTool.Catalog;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class InventorySelectionStateTests
{
    [Fact]
    public void InventoryEditabilityTreatsLegacyPlaceholdersAndCatalogBlankItemsAsUnwritable()
    {
        ushort[] blankItemIds = P4GCatalog.Items
            .Where(static item => string.Equals(item.Name, "Blank", StringComparison.Ordinal) && item.Id < 2559)
            .Select(static item => item.Id)
            .ToArray();

        Assert.NotEmpty(blankItemIds);
        Assert.Contains((ushort)1792, InventoryItemEditability.PlaceholderItemIds);
        Assert.All(blankItemIds, static itemId => Assert.False(InventoryItemEditability.IsWritableItemId(itemId)));
    }

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
    public void InventoryQuantityDraftRestoresThroughSuccessfulApplySaveAcknowledgeAndReportRefreshes()
    {
        const string quantityDraft = "37";
        byte? selectedCategoryIdBeforeRefresh = (byte)ItemCategoryId.Other;
        ushort? selectedItemIdBeforeRefresh = 1025;
        ushort? selectedEntryIdBeforeRefresh = 1025;
        string inventoryQuantityText = string.Empty;

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            "1");
        Assert.Equal(quantityDraft, inventoryQuantityText);

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            "2");
        Assert.Equal(quantityDraft, inventoryQuantityText);

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            "3");
        Assert.Equal(quantityDraft, inventoryQuantityText);

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            selectedCategoryIdBeforeRefresh,
            selectedItemIdBeforeRefresh,
            selectedEntryIdBeforeRefresh,
            "4");
        Assert.Equal(quantityDraft, inventoryQuantityText);
    }

    [Fact]
    public void InventoryQuantityDraftIsNotRestoredAfterSelectionContextChanges()
    {
        const string quantityDraft = "37";
        string inventoryQuantityText = "1";

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            (byte)ItemCategoryId.Other,
            1025,
            1025,
            (byte)ItemCategoryId.Weapons,
            1025,
            1025,
            inventoryQuantityText);
        Assert.Equal("1", inventoryQuantityText);

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            (byte)ItemCategoryId.Other,
            1025,
            1025,
            (byte)ItemCategoryId.Other,
            1026,
            1026,
            inventoryQuantityText);
        Assert.Equal("1", inventoryQuantityText);

        inventoryQuantityText = SimulateInventoryQuantityRefresh(
            quantityDraft,
            (byte)ItemCategoryId.Other,
            1025,
            1025,
            (byte)ItemCategoryId.Other,
            1025,
            null,
            inventoryQuantityText);
        Assert.Equal("1", inventoryQuantityText);
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

    [Fact]
    public void RemembersLastSelectedItemPerCategory()
    {
        InventorySelectionState selectionState = new();

        selectionState.RememberCategoryItem((byte)ItemCategoryId.Weapons, 2432);
        selectionState.RememberCategoryItem((byte)ItemCategoryId.Consumables, 0);

        Assert.Equal((ushort)2432, selectionState.GetRememberedCategoryItem((byte)ItemCategoryId.Weapons));
        Assert.Equal((ushort)0, selectionState.GetRememberedCategoryItem((byte)ItemCategoryId.Consumables));
        Assert.Null(selectionState.GetRememberedCategoryItem((byte)ItemCategoryId.Other));
    }

    [Fact]
    public void ResetClearsRememberedCategoryItems()
    {
        InventorySelectionState selectionState = new();
        selectionState.RememberCategoryItem((byte)ItemCategoryId.Weapons, 2432);

        selectionState.Reset();

        Assert.Null(selectionState.GetRememberedCategoryItem((byte)ItemCategoryId.Weapons));
    }

    private static string SimulateInventoryQuantityRefresh(
        string quantityDraft,
        byte? selectedCategoryIdBeforeRefresh,
        ushort? selectedItemIdBeforeRefresh,
        ushort? selectedEntryIdBeforeRefresh,
        byte? selectedCategoryIdAfterRefresh,
        ushort? selectedItemIdAfterRefresh,
        ushort? selectedEntryIdAfterRefresh,
        string hydratedQuantityText)
    {
        string refreshedQuantityText = hydratedQuantityText;

        if (InventorySelectionState.ShouldRestoreQuantityDraft(
                selectedCategoryIdBeforeRefresh,
                selectedItemIdBeforeRefresh,
                selectedEntryIdBeforeRefresh,
                selectedCategoryIdAfterRefresh,
                selectedItemIdAfterRefresh,
                selectedEntryIdAfterRefresh))
        {
            refreshedQuantityText = quantityDraft;
        }

        return refreshedQuantityText;
    }
}
