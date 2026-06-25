using P4G.SaveTool.Catalog;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class InventorySelectionProjectionTests
{
    [Fact]
    public void UnselectedCatalogChoiceIsPreservedWhenNoInventoryEntryExists()
    {
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices =
        [
            new InventoryItemChoiceViewState(1024, (byte)ItemCategoryId.Other, "Blank", true),
            new InventoryItemChoiceViewState(2432, (byte)ItemCategoryId.Weapons, "Weapon 2432"),
        ];

        IReadOnlyList<InventoryItemChoiceViewState> projectedChoices =
            InventorySelectionProjection.ResolveItemChoices(itemChoices, null, 2432, out InventoryItemChoiceViewState? selectedItem);

        Assert.NotNull(selectedItem);
        Assert.Equal((ushort)2432, selectedItem!.ItemId);
        Assert.False(selectedItem.IsPlaceholder);
        Assert.Equal("Weapon 2432", selectedItem.Name);
        Assert.Same(itemChoices, projectedChoices);
        Assert.Equal(2, projectedChoices.Count);

        InventorySelectionProjection.ResolveItemChoices(itemChoices, null, null, out InventoryItemChoiceViewState? defaultSelectedItem);

        Assert.NotNull(defaultSelectedItem);
        Assert.Equal((ushort)1024, defaultSelectedItem!.ItemId);
    }

    [Fact]
    public void OtherCatalogChoiceIsResolvedWithoutSynthesizingARawEntry()
    {
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices =
        [
            new InventoryItemChoiceViewState(1024, (byte)ItemCategoryId.Other, "Blank", true),
            new InventoryItemChoiceViewState(820, (byte)ItemCategoryId.Other, "Arc Magatama"),
        ];

        InventoryStackViewState selectedEntry = new(0, 820, "Arc Magatama", (byte)ItemCategoryId.Other, "Other", 1);

        IReadOnlyList<InventoryItemChoiceViewState> projectedChoices =
            InventorySelectionProjection.ResolveItemChoices(itemChoices, selectedEntry, 820, out InventoryItemChoiceViewState? selectedItem);

        Assert.NotNull(selectedItem);
        Assert.Equal((ushort)820, selectedItem!.ItemId);
        Assert.Equal("Arc Magatama", selectedItem.Name);
        Assert.Same(itemChoices, projectedChoices);
    }
}
