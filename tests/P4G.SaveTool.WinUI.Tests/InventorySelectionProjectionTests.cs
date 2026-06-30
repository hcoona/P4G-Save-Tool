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

    [Fact]
    public void UnknownInventoryEntryIsSynthesizedWhenMissingFromItemChoices()
    {
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices =
        [
            new InventoryItemChoiceViewState(0, (byte)ItemCategoryId.Weapons, "Blank", true),
            new InventoryItemChoiceViewState(1, (byte)ItemCategoryId.Weapons, "Blade"),
        ];

        InventoryStackViewState selectedEntry = new(7, 9999, "Unknown item (9999)", (byte)ItemCategoryId.Weapons, "Weapons", 3);

        IReadOnlyList<InventoryItemChoiceViewState> projectedChoices =
            InventorySelectionProjection.ResolveItemChoices(itemChoices, selectedEntry, null, out InventoryItemChoiceViewState? selectedItem);

        Assert.NotNull(selectedItem);
        Assert.Equal((ushort)9999, selectedItem!.ItemId);
        Assert.Equal("Unknown item (9999)", selectedItem.Name);
        Assert.Equal((byte)ItemCategoryId.Weapons, selectedItem.CategoryId);
        Assert.False(selectedItem.IsPlaceholder);
        Assert.Equal(itemChoices.Count + 1, projectedChoices.Count);
        Assert.Same(selectedItem, projectedChoices[^1]);
        Assert.Contains(projectedChoices, item => item.ItemId == 9999);
    }

    [Fact]
    public void EquipmentChoiceResolverKeepsUnsupportedCurrentSelectionVisible()
    {
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices =
        [
            new InventoryItemChoiceViewState(0, (byte)ItemCategoryId.Weapons, "Blank", true),
            new InventoryItemChoiceViewState(1, (byte)ItemCategoryId.Weapons, "Blade"),
        ];

        IReadOnlyList<InventoryItemChoiceViewState> projectedChoices =
            InventorySelectionProjection.ResolveEquipmentChoices(itemChoices, 9999, out InventoryItemChoiceViewState? selectedItem);

        Assert.NotNull(selectedItem);
        Assert.Equal((ushort)9999, selectedItem!.ItemId);
        Assert.Equal("Unknown item (9999)", selectedItem.Name);
        Assert.Contains(projectedChoices, item => item.ItemId == 9999);
        Assert.Equal(itemChoices.Count + 1, projectedChoices.Count);
    }

    [Fact]
    public void EquipmentChoiceResolverMapsRawZeroToLegacyBlankChoice()
    {
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices =
        [
            new InventoryItemChoiceViewState(256, (byte)ItemCategoryId.Armor, "Blank", true),
            new InventoryItemChoiceViewState(257, (byte)ItemCategoryId.Armor, "Chain Mail"),
        ];

        IReadOnlyList<InventoryItemChoiceViewState> projectedChoices =
            InventorySelectionProjection.ResolveEquipmentChoices(itemChoices, 0, out InventoryItemChoiceViewState? selectedItem);

        Assert.NotNull(selectedItem);
        Assert.Same(itemChoices, projectedChoices);
        Assert.Equal((ushort)256, selectedItem!.ItemId);
        Assert.Equal("Blank", selectedItem.Name);
        Assert.True(selectedItem.IsPlaceholder);
    }

    [Fact]
    public void EquipmentChoiceResolverDoesNotDuplicateSupportedSelection()
    {
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices =
        [
            new InventoryItemChoiceViewState(0, (byte)ItemCategoryId.Weapons, "Blank", true),
            new InventoryItemChoiceViewState(1, (byte)ItemCategoryId.Weapons, "Blade"),
        ];

        IReadOnlyList<InventoryItemChoiceViewState> projectedChoices =
            InventorySelectionProjection.ResolveEquipmentChoices(itemChoices, 1, out InventoryItemChoiceViewState? selectedItem);

        Assert.NotNull(selectedItem);
        Assert.Same(itemChoices, projectedChoices);
        Assert.Equal((ushort)1, selectedItem!.ItemId);
        Assert.Equal("Blade", selectedItem.Name);
    }
}
