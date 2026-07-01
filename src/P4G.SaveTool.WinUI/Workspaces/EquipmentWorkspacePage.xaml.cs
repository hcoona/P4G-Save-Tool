using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

public sealed partial class EquipmentWorkspacePage : Page
{
    public EquipmentWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    internal event SelectionChangedEventHandler? EquipmentCharacterSelectionChanged;
    internal event SelectionChangedEventHandler? EquipmentWeaponSelectionChanged;
    internal event SelectionChangedEventHandler? EquipmentArmorSelectionChanged;
    internal event SelectionChangedEventHandler? EquipmentAccessorySelectionChanged;
    internal event SelectionChangedEventHandler? EquipmentCostumeSelectionChanged;

    internal EquipmentCharacterViewState? SelectedCharacter
    {
        get => EquipmentCharacterComboBox.SelectedItem as EquipmentCharacterViewState;
        set => EquipmentCharacterComboBox.SelectedItem = value;
    }

    internal InventoryItemChoiceViewState? SelectedWeapon
    {
        get => EquipmentWeaponComboBox.SelectedItem as InventoryItemChoiceViewState;
        set => EquipmentWeaponComboBox.SelectedItem = value;
    }

    internal InventoryItemChoiceViewState? SelectedArmor
    {
        get => EquipmentArmorComboBox.SelectedItem as InventoryItemChoiceViewState;
        set => EquipmentArmorComboBox.SelectedItem = value;
    }

    internal InventoryItemChoiceViewState? SelectedAccessory
    {
        get => EquipmentAccessoryComboBox.SelectedItem as InventoryItemChoiceViewState;
        set => EquipmentAccessoryComboBox.SelectedItem = value;
    }

    internal InventoryItemChoiceViewState? SelectedCostume
    {
        get => EquipmentCostumeComboBox.SelectedItem as InventoryItemChoiceViewState;
        set => EquipmentCostumeComboBox.SelectedItem = value;
    }

    internal void SetItemsSources(
        object charactersSource,
        object weaponChoicesSource,
        object armorChoicesSource,
        object accessoryChoicesSource,
        object costumeChoicesSource)
    {
        EquipmentCharacterComboBox.ItemsSource = charactersSource;
        EquipmentWeaponComboBox.ItemsSource = weaponChoicesSource;
        EquipmentArmorComboBox.ItemsSource = armorChoicesSource;
        EquipmentAccessoryComboBox.ItemsSource = accessoryChoicesSource;
        EquipmentCostumeComboBox.ItemsSource = costumeChoicesSource;
    }

    internal void SetEquipmentEnabled(bool isEnabled)
    {
        EquipmentCharacterComboBox.IsEnabled = isEnabled;
        EquipmentWeaponComboBox.IsEnabled = isEnabled;
        EquipmentArmorComboBox.IsEnabled = isEnabled;
        EquipmentAccessoryComboBox.IsEnabled = isEnabled;
        EquipmentCostumeComboBox.IsEnabled = isEnabled;
    }

    private void EquipmentCharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        EquipmentCharacterSelectionChanged?.Invoke(sender, e);

    private void EquipmentWeaponComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        EquipmentWeaponSelectionChanged?.Invoke(sender, e);

    private void EquipmentArmorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        EquipmentArmorSelectionChanged?.Invoke(sender, e);

    private void EquipmentAccessoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        EquipmentAccessorySelectionChanged?.Invoke(sender, e);

    private void EquipmentCostumeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        EquipmentCostumeSelectionChanged?.Invoke(sender, e);
}
