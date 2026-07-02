using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

public sealed partial class InventoryWorkspacePage : Page
{
    public InventoryWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    internal event SelectionChangedEventHandler? InventorySelectionChanged;
    internal event SelectionChangedEventHandler? InventoryCategorySelectionChanged;
    internal event SelectionChangedEventHandler? InventoryItemSelectionChanged;
    internal event TextChangedEventHandler? InventoryQuantityTextChanged;
    internal event RoutedEventHandler? InventoryAddUpdateClick;
    internal event RoutedEventHandler? InventoryDeleteClick;

    internal InventoryStackViewState? SelectedInventoryEntry
    {
        get => InventoryListView.SelectedItem as InventoryStackViewState;
        set => InventoryListView.SelectedItem = value;
    }

    internal ItemCategoryViewState? SelectedCategory
    {
        get => InventoryCategoryComboBox.SelectedItem as ItemCategoryViewState;
        set => InventoryCategoryComboBox.SelectedItem = value;
    }

    internal InventoryItemChoiceViewState? SelectedItem
    {
        get => InventoryItemComboBox.SelectedItem as InventoryItemChoiceViewState;
        set => InventoryItemComboBox.SelectedItem = value;
    }

    internal string QuantityText
    {
        get => InventoryQuantityTextBox.Text ?? string.Empty;
        set => InventoryQuantityTextBox.Text = value;
    }

    internal string SelectedInventoryEntryDescription =>
        InventoryListView.SelectedItem?.ToString() ?? "the selected inventory entry";

    internal void SetItemsSources(
        object inventoryEntriesSource,
        object categoriesSource,
        object itemChoicesSource)
    {
        InventoryListView.ItemsSource = inventoryEntriesSource;
        InventoryCategoryComboBox.ItemsSource = categoriesSource;
        InventoryItemComboBox.ItemsSource = itemChoicesSource;
    }

    internal void SetInventoryEnabled(bool isEnabled, bool hasSelectedItem, bool hasSelectedEntry)
    {
        InventoryListView.IsEnabled = isEnabled;
        InventoryCategoryComboBox.IsEnabled = isEnabled;
        InventoryItemComboBox.IsEnabled = isEnabled;
        InventoryQuantityTextBox.IsEnabled = isEnabled && hasSelectedItem;
        InventoryAddUpdateButton.IsEnabled = isEnabled && hasSelectedItem;
        InventoryDeleteButton.IsEnabled = isEnabled && hasSelectedEntry && hasSelectedItem;
    }

    internal void SetAddUpdateButtonText(string text) =>
        InventoryAddUpdateButton.Content = text;

    private void InventoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        InventorySelectionChanged?.Invoke(sender, e);

    private void InventoryCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        InventoryCategorySelectionChanged?.Invoke(sender, e);

    private void InventoryItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        InventoryItemSelectionChanged?.Invoke(sender, e);

    private void InventoryQuantityTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        InventoryQuantityTextChanged?.Invoke(sender, e);

    private void InventoryAddUpdateButton_Click(object sender, RoutedEventArgs e) =>
        InventoryAddUpdateClick?.Invoke(sender, e);

    private void InventoryDeleteButton_Click(object sender, RoutedEventArgs e) =>
        InventoryDeleteClick?.Invoke(sender, e);
}
