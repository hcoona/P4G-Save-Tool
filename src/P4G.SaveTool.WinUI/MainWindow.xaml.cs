using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using P4G.SaveTool.Application;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace P4G.SaveTool.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly SaveEditorViewModel viewModel;
    private readonly InventorySelectionState inventorySelectionState = new();
    private IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride;
    private string? currentFilePath;
    private bool isBusy;
    private bool suppressInventoryEvents;
    private bool suppressEquipmentEvents;
    private bool preserveEditorTextDuringInventoryRefresh;
    private bool autoSelectInventoryEntryAfterOpen;
    private byte? selectedInventoryCategoryId;
    private ushort? selectedInventoryItemId;
    private ushort? selectedInventoryEntryId;
    private byte? selectedEquipmentCharacterId;

    public MainWindow()
    {
        InitializeComponent();

        viewModel = new SaveEditorViewModel(new SaveApplicationService());
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RefreshFromViewModel();
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(OpenSaveFileAsync);

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ApplyEditorFields();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(() => SaveAsync(forcePicker: false));

    private async void SaveAsButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(() => SaveAsync(forcePicker: true));

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (preserveEditorTextDuringInventoryRefresh)
        {
            RefreshInventoryState();
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return;
        }

        RefreshFromViewModel();
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        UpdateShellState();
        try
        {
            await operation();
        }
        finally
        {
            isBusy = false;
            RefreshFromViewModel();
        }
    }

    private async Task OpenSaveFileAsync()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".bin");
        picker.FileTypeFilter.Add(".sav");
        picker.FileTypeFilter.Add(".dat");
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(file.Path))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI001", "The selected file does not expose a local path.", "Open")]);
            return;
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(file.Path);
            uiDiagnosticsOverride = null;
            SaveEditorOperationResult result = viewModel.OpenSave(bytes);
            if (result.Succeeded)
            {
                currentFilePath = file.Path;
                selectedInventoryCategoryId = null;
                selectedInventoryItemId = null;
                selectedInventoryEntryId = null;
                selectedEquipmentCharacterId = null;
                inventorySelectionState.Reset();
                autoSelectInventoryEntryAfterOpen = true;
                InventoryQuantityTextBox.Text = string.Empty;
            }

            RefreshFromViewModel();
            if (!result.Succeeded)
            {
                await ShowMessageAsync("Open failed", FormatDiagnostics(result.Diagnostics));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IReadOnlyList<SaveDiagnostic> diagnostics =
            [
                CreateUiDiagnostic("P4GWINUI002", $"Could not read the selected file: {ex.Message}", "Open"),
            ];
            SetUiDiagnostics(diagnostics);
            await ShowMessageAsync("Open failed", FormatDiagnostics(diagnostics));
        }
    }

    private bool ApplyEditorFields()
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI003", "Open a save before applying edits.", "Edit")]);
            return false;
        }

        if (!TryReadEditorValues(
            out string familyName,
            out string givenName,
            out uint yen,
            out IReadOnlyList<byte> partyMemberValues,
            out IReadOnlyList<SaveDiagnostic> diagnostics))
        {
            SetUiDiagnostics(diagnostics);
            return false;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = viewModel.ApplyEditorValues(familyName, givenName, yen, partyMemberValues);
        RefreshFromViewModel();
        return result.Succeeded;
    }

    private async Task SaveAsync(bool forcePicker)
    {
        if (!ApplyEditorFields())
        {
            return;
        }

        string? targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)
            ? await PickSavePathAsync()
            : currentFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        RefreshFromViewModel();
        if (!writeResult.Succeeded || writeResult.Bytes is null || writeResult.OperationToken is null)
        {
            await ShowMessageAsync("Save failed", FormatDiagnostics(writeResult.Diagnostics));
            return;
        }

        SaveEditorWriteToken operationToken = writeResult.OperationToken.Value;
        try
        {
            await SafeFilePersistence.ReplaceFileAsync(targetPath, writeResult.Bytes);
        }
        catch (Exception ex) when (IsPersistenceException(ex))
        {
            IReadOnlyList<SaveDiagnostic> diagnostics =
            [
                CreateUiDiagnostic("P4GWINUI004", $"Could not write the save file: {ex.Message}", "Persistence"),
            ];
            SaveEditorOperationResult reportResult = viewModel.ReportSaveFailed(operationToken, diagnostics);
            RefreshFromViewModel();
            await ShowMessageAsync("Save failed", FormatDiagnostics(reportResult.Diagnostics));
            return;
        }

        currentFilePath = targetPath;
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);
        RefreshFromViewModel();
        if (!acknowledgeResult.Succeeded)
        {
            await ShowMessageAsync("Save acknowledgement failed", FormatDiagnostics(acknowledgeResult.Diagnostics));
        }
    }

    private async Task<string?> PickSavePathAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = string.IsNullOrWhiteSpace(currentFilePath)
                ? "data0001"
                : Path.GetFileNameWithoutExtension(currentFilePath),
        };
        picker.FileTypeChoices.Add("Persona 4 Golden save", new List<string> { ".bin" });
        picker.FileTypeChoices.Add("Save file", new List<string> { ".sav" });
        picker.FileTypeChoices.Add("Data file", new List<string> { ".dat" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(file.Path))
        {
            return file.Path;
        }

        SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI005", "The selected save target does not expose a local path.", "Save")]);
        return null;
    }

    private bool TryReadEditorValues(
        out string familyName,
        out string givenName,
        out uint yen,
        out IReadOnlyList<byte> partyMemberValues,
        out IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        List<SaveDiagnostic> validationDiagnostics = [];
        List<byte> parsedPartyMemberValues = [];

        familyName = FamilyNameTextBox.Text ?? string.Empty;
        givenName = GivenNameTextBox.Text ?? string.Empty;

        if (uint.TryParse(YenTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedYen))
        {
            yen = parsedYen;
        }
        else
        {
            yen = 0;
            validationDiagnostics.Add(CreateUiDiagnostic("P4GWINUI006", "Yen must be an unsigned whole number.", "Yen"));
        }

        AddPartyMemberValue(PartySlot0TextBox, 0, parsedPartyMemberValues, validationDiagnostics);
        AddPartyMemberValue(PartySlot1TextBox, 1, parsedPartyMemberValues, validationDiagnostics);
        AddPartyMemberValue(PartySlot2TextBox, 2, parsedPartyMemberValues, validationDiagnostics);

        partyMemberValues = validationDiagnostics.Count == 0 ? parsedPartyMemberValues : [];
        diagnostics = validationDiagnostics;
        return validationDiagnostics.Count == 0;
    }

    private static void AddPartyMemberValue(
        TextBox textBox,
        int slotIndex,
        List<byte> partyMemberValues,
        List<SaveDiagnostic> diagnostics)
    {
        if (byte.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte memberValue))
        {
            partyMemberValues.Add(memberValue);
            return;
        }

        diagnostics.Add(CreateUiDiagnostic(
            "P4GWINUI007",
            $"Party slot {slotIndex + 1} must be a whole number from 0 to 255.",
            $"PartyMembers[{slotIndex}]"));
    }

    private void RefreshFromViewModel()
    {
        RefreshEditableFields();
        RefreshInventoryState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
    }

    private void RefreshEditableFields()
    {
        FamilyNameTextBox.Text = viewModel.FamilyName;
        GivenNameTextBox.Text = viewModel.GivenName;
        YenTextBox.Text = viewModel.HasSave ? viewModel.Yen.ToString(CultureInfo.InvariantCulture) : string.Empty;
        PartySlot0TextBox.Text = GetPartyMemberValue(0);
        PartySlot1TextBox.Text = GetPartyMemberValue(1);
        PartySlot2TextBox.Text = GetPartyMemberValue(2);
        PersonaSummaryTextBox.Text = BuildPersonaSummary();
        RefreshEquipmentState();
    }

    private void UpdateShellState()
    {
        bool canEdit = viewModel.HasSave && !isBusy;
        bool canSave = canEdit && viewModel.CanWrite;

        OpenButton.IsEnabled = !isBusy;
        ApplyButton.IsEnabled = canEdit;
        SaveButton.IsEnabled = canSave;
        SaveAsButton.IsEnabled = canSave;
        FamilyNameTextBox.IsEnabled = canEdit;
        GivenNameTextBox.IsEnabled = canEdit;
        YenTextBox.IsEnabled = canEdit;
        PartySlot0TextBox.IsEnabled = canEdit;
        PartySlot1TextBox.IsEnabled = canEdit;
        PartySlot2TextBox.IsEnabled = canEdit;
        EquipmentCharacterComboBox.IsEnabled = canEdit;
        EquipmentWeaponComboBox.IsEnabled = canEdit;
        EquipmentArmorComboBox.IsEnabled = canEdit;
        EquipmentAccessoryComboBox.IsEnabled = canEdit;
        EquipmentCostumeComboBox.IsEnabled = canEdit;
        InventoryListView.IsEnabled = canEdit;
        InventoryCategoryComboBox.IsEnabled = canEdit;
        InventoryItemComboBox.IsEnabled = canEdit;
        InventoryQuantityTextBox.IsEnabled = canEdit;
        InventoryAddUpdateButton.IsEnabled = canEdit && selectedInventoryItemId.HasValue;
        InventoryDeleteButton.IsEnabled = canEdit && selectedInventoryEntryId.HasValue && selectedInventoryItemId.HasValue;

        FilePathTextBlock.Text = string.IsNullOrWhiteSpace(currentFilePath)
            ? "No save file is open."
            : currentFilePath;
        StateTextBlock.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Has save: {FormatBoolean(viewModel.HasSave)} | Dirty: {FormatBoolean(viewModel.IsDirty)} | Can write: {FormatBoolean(viewModel.CanWrite)}");
    }

    private void RefreshInventoryState()
    {
        suppressInventoryEvents = true;
        try
        {
            InventoryListView.ItemsSource = viewModel.HasSave
                ? viewModel.InventoryEntries
                : Array.Empty<InventoryStackViewState>();
            InventoryCategoryComboBox.ItemsSource = viewModel.InventoryCategories;

            if (!viewModel.HasSave || viewModel.InventoryCategories.Count == 0)
            {
                autoSelectInventoryEntryAfterOpen = false;
                selectedInventoryCategoryId = null;
                selectedInventoryItemId = null;
                selectedInventoryEntryId = null;
                InventoryCategoryComboBox.SelectedItem = null;
                InventoryItemComboBox.ItemsSource = Array.Empty<InventoryItemChoiceViewState>();
                InventoryItemComboBox.SelectedItem = null;
                InventoryListView.SelectedItem = null;
                if (inventorySelectionState.ShouldHydrateQuantityText(null, null, null, string.Empty))
                {
                    InventoryQuantityTextBox.Text = string.Empty;
                }
                InventoryAddUpdateButton.Content = "Add/Update";
                InventoryDeleteButton.IsEnabled = false;
                return;
            }

            InventoryStackViewState? selectedEntry = null;
            bool shouldAutoSelectInventoryEntryAfterOpen = autoSelectInventoryEntryAfterOpen;
            autoSelectInventoryEntryAfterOpen = false;
            if (selectedInventoryEntryId.HasValue)
            {
                selectedEntry = viewModel.InventoryEntries.FirstOrDefault(entry => entry.ItemId == selectedInventoryEntryId.Value);
                if (selectedEntry is not null)
                {
                    selectedInventoryCategoryId = selectedEntry.CategoryId;
                    selectedInventoryItemId = selectedEntry.IsPlaceholder ? null : selectedEntry.ItemId;
                }
            }

            if (selectedEntry is null &&
                shouldAutoSelectInventoryEntryAfterOpen &&
                inventorySelectionState.ShouldAutoSelectFirstEntry(
                    viewModel.HasSave,
                    viewModel.InventoryEntries,
                    selectedInventoryCategoryId,
                    selectedInventoryItemId,
                    selectedInventoryEntryId))
            {
                int lastInventoryEntryIndex = viewModel.InventoryEntries.Count - 1;
                selectedEntry = viewModel.InventoryEntries[lastInventoryEntryIndex];
                selectedInventoryCategoryId = selectedEntry.CategoryId;
                selectedInventoryItemId = selectedEntry.IsPlaceholder ? null : selectedEntry.ItemId;
                selectedInventoryEntryId = selectedEntry.ItemId;
            }

            ItemCategoryViewState? selectedCategory = selectedInventoryCategoryId.HasValue
                ? viewModel.InventoryCategories.FirstOrDefault(category => category.CategoryId == selectedInventoryCategoryId.Value)
                : null;
            if (selectedCategory is null && selectedEntry is not null)
            {
                selectedCategory = viewModel.InventoryCategories.FirstOrDefault(category => category.CategoryId == selectedEntry.CategoryId);
            }

            if (selectedCategory is null && selectedEntry is null && selectedInventoryCategoryId is null && selectedInventoryItemId is null)
            {
                InventoryCategoryComboBox.SelectedItem = null;
                InventoryItemComboBox.ItemsSource = Array.Empty<InventoryItemChoiceViewState>();
                InventoryItemComboBox.SelectedItem = null;
                InventoryListView.SelectedItem = null;
                if (inventorySelectionState.ShouldHydrateQuantityText(null, null, null, string.Empty))
                {
                    InventoryQuantityTextBox.Text = string.Empty;
                }
                InventoryAddUpdateButton.Content = "Add/Update";
                InventoryDeleteButton.IsEnabled = false;
                return;
            }

            InventoryCategoryComboBox.SelectedItem = selectedCategory;

            IReadOnlyList<InventoryItemChoiceViewState> itemChoices = selectedCategory is not null
                ? viewModel.GetInventoryItemsForCategory(selectedCategory.CategoryId)
                : Array.Empty<InventoryItemChoiceViewState>();
            itemChoices = InventorySelectionProjection.ResolveItemChoices(
                itemChoices,
                selectedEntry,
                selectedInventoryItemId,
                out InventoryItemChoiceViewState? selectedItem);

            InventoryItemComboBox.ItemsSource = itemChoices;
            InventoryItemComboBox.SelectedItem = selectedItem;
            selectedInventoryItemId = selectedItem is { IsPlaceholder: false } ? selectedItem.ItemId : null;

            InventoryListView.SelectedItem = selectedEntry;
            selectedInventoryEntryId = selectedEntry?.ItemId;
            string inventoryQuantityText = selectedEntry?.Quantity.ToString(CultureInfo.InvariantCulture)
                ?? (selectedItem is null || selectedItem.IsPlaceholder
                    ? string.Empty
                    : GetInventoryQuantityOrDefault(selectedItem.ItemId).ToString(CultureInfo.InvariantCulture));
            if (inventorySelectionState.ShouldHydrateQuantityText(
                selectedInventoryCategoryId,
                selectedInventoryItemId,
                selectedInventoryEntryId,
                inventoryQuantityText))
            {
                InventoryQuantityTextBox.Text = inventoryQuantityText;
            }
            InventoryAddUpdateButton.Content = selectedItem is null || selectedItem.IsPlaceholder || selectedEntry is null ? "Add/Update" : "Update";
            InventoryDeleteButton.IsEnabled = selectedEntry is not null && InventoryListView.IsEnabled && selectedInventoryItemId.HasValue;
        }
        finally
        {
            suppressInventoryEvents = false;
        }
    }

    private void RefreshEquipmentState()
    {
        suppressEquipmentEvents = true;
        try
        {
            EquipmentCharacterComboBox.ItemsSource = viewModel.HasSave
                ? viewModel.EquipmentCharacters
                : Array.Empty<EquipmentCharacterViewState>();

            if (!viewModel.HasSave || viewModel.EquipmentCharacters.Count == 0)
            {
                selectedEquipmentCharacterId = null;
                EquipmentCharacterComboBox.SelectedItem = null;
                EquipmentWeaponComboBox.ItemsSource = Array.Empty<InventoryItemChoiceViewState>();
                EquipmentArmorComboBox.ItemsSource = Array.Empty<InventoryItemChoiceViewState>();
                EquipmentAccessoryComboBox.ItemsSource = Array.Empty<InventoryItemChoiceViewState>();
                EquipmentCostumeComboBox.ItemsSource = Array.Empty<InventoryItemChoiceViewState>();
                EquipmentWeaponComboBox.SelectedItem = null;
                EquipmentArmorComboBox.SelectedItem = null;
                EquipmentAccessoryComboBox.SelectedItem = null;
                EquipmentCostumeComboBox.SelectedItem = null;
                return;
            }

            EquipmentCharacterViewState? selectedCharacter = null;
            if (selectedEquipmentCharacterId.HasValue)
            {
                selectedCharacter = viewModel.EquipmentCharacters.FirstOrDefault(
                    character => character.CharacterId == selectedEquipmentCharacterId.Value);
            }

            selectedCharacter ??= viewModel.EquipmentCharacters[0];
            EquipmentCharacterComboBox.SelectedItem = selectedCharacter;
            selectedEquipmentCharacterId = selectedCharacter.CharacterId;

            SetEquipmentChoices(EquipmentWeaponComboBox, viewModel.GetWeaponChoices(selectedCharacter.CharacterId), selectedCharacter.WeaponItemId);
            SetEquipmentChoices(EquipmentArmorComboBox, viewModel.GetArmorChoices(), selectedCharacter.ArmorItemId);
            SetEquipmentChoices(EquipmentAccessoryComboBox, viewModel.GetAccessoryChoices(), selectedCharacter.AccessoryItemId);
            SetEquipmentChoices(EquipmentCostumeComboBox, viewModel.GetCostumeChoices(), selectedCharacter.CostumeItemId);
        }
        finally
        {
            suppressEquipmentEvents = false;
        }
    }

    private static void SetEquipmentChoices(
        ComboBox comboBox,
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices,
        ushort selectedItemId)
    {
        comboBox.ItemsSource = InventorySelectionProjection.ResolveEquipmentChoices(
            itemChoices,
            selectedItemId,
            out InventoryItemChoiceViewState? selectedItem);
        comboBox.SelectedItem = selectedItem;
    }

    private byte GetInventoryQuantityOrDefault(ushort itemId)
    {
        InventoryStackViewState? entry = viewModel.InventoryEntries.FirstOrDefault(stack => stack.ItemId == itemId);
        return entry?.Quantity ?? (byte)1;
    }

    private void InventoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressInventoryEvents)
        {
            return;
        }

        if (InventoryListView.SelectedItem is not InventoryStackViewState selectedEntry)
        {
            selectedInventoryEntryId = null;
            UpdateShellState();
            return;
        }

        selectedInventoryEntryId = selectedEntry.ItemId;
        selectedInventoryCategoryId = selectedEntry.CategoryId;
        selectedInventoryItemId = selectedEntry.IsPlaceholder ? null : selectedEntry.ItemId;
        RefreshInventoryState();
        UpdateShellState();
    }

    private void InventoryCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressInventoryEvents)
        {
            return;
        }

        if (InventoryCategoryComboBox.SelectedItem is ItemCategoryViewState selectedCategory)
        {
            selectedInventoryCategoryId = selectedCategory.CategoryId;
            selectedInventoryItemId = null;
            selectedInventoryEntryId = null;
            RefreshInventoryState();
            UpdateShellState();
        }
    }

    private void InventoryItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressInventoryEvents)
        {
            return;
        }

        if (InventoryItemComboBox.SelectedItem is InventoryItemChoiceViewState selectedItem)
        {
            selectedInventoryCategoryId = selectedItem.CategoryId;
            selectedInventoryItemId = selectedItem.IsPlaceholder ? null : selectedItem.ItemId;
            selectedInventoryEntryId = selectedItem.IsPlaceholder
                ? null
                : viewModel.InventoryEntries.FirstOrDefault(entry => entry.ItemId == selectedItem.ItemId)?.ItemId;
            RefreshInventoryState();
            UpdateShellState();
            return;
        }

        selectedInventoryItemId = null;
        selectedInventoryEntryId = null;
        RefreshInventoryState();
        UpdateShellState();
    }

    private void InventoryAddUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI008", "Open a save before editing inventory.", "Inventory")]);
            return;
        }

        if (!selectedInventoryItemId.HasValue)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI009", "Select an inventory item before saving its quantity.", "Inventory.Item")]);
            return;
        }

        if (!TryReadInventoryQuantity(out byte quantity))
        {
            return;
        }

        uiDiagnosticsOverride = null;
        preserveEditorTextDuringInventoryRefresh = true;
        SaveEditorOperationResult result;
        try
        {
            result = viewModel.SetInventoryItemQuantity(selectedInventoryItemId.Value, quantity);
        }
        finally
        {
            preserveEditorTextDuringInventoryRefresh = false;
        }

        if (result.Succeeded)
        {
            InventorySelectionState.ApplySuccessfulQuantityEdit(
                quantity,
                ref selectedInventoryCategoryId,
                ref selectedInventoryItemId,
                ref selectedInventoryEntryId);

            if (quantity == 0)
            {
                inventorySelectionState.DisableAutoSelectAfterDelete();
            }
        }
        RefreshInventoryState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            _ = ShowMessageAsync("Inventory update failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void InventoryDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI008", "Open a save before editing inventory.", "Inventory")]);
            return;
        }

        if (!selectedInventoryEntryId.HasValue)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI010", "Select an inventory entry before deleting it.", "Inventory")]);
            return;
        }

        uiDiagnosticsOverride = null;
        preserveEditorTextDuringInventoryRefresh = true;
        SaveEditorOperationResult result;
        try
        {
            result = viewModel.RemoveInventoryItem(selectedInventoryEntryId.Value);
        }
        finally
        {
            preserveEditorTextDuringInventoryRefresh = false;
        }

        if (result.Succeeded)
        {
            inventorySelectionState.DisableAutoSelectAfterDelete();
            selectedInventoryCategoryId = null;
            selectedInventoryItemId = null;
            selectedInventoryEntryId = null;
        }
        RefreshInventoryState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            _ = ShowMessageAsync("Inventory delete failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void EquipmentCharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressEquipmentEvents)
        {
            return;
        }

        if (EquipmentCharacterComboBox.SelectedItem is not EquipmentCharacterViewState selectedCharacter)
        {
            selectedEquipmentCharacterId = null;
            UpdateShellState();
            return;
        }

        selectedEquipmentCharacterId = selectedCharacter.CharacterId;
        RefreshEquipmentState();
        UpdateShellState();
    }

    private void EquipmentWeaponComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyEquipmentSelection(EquipmentWeaponComboBox, static (viewModel, characterId, itemId) => viewModel.SetEquippedWeapon(characterId, itemId), "Equipment.Weapon");

    private void EquipmentArmorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyEquipmentSelection(EquipmentArmorComboBox, static (viewModel, characterId, itemId) => viewModel.SetEquippedArmor(characterId, itemId), "Equipment.Armor");

    private void EquipmentAccessoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyEquipmentSelection(EquipmentAccessoryComboBox, static (viewModel, characterId, itemId) => viewModel.SetEquippedAccessory(characterId, itemId), "Equipment.Accessory");

    private void EquipmentCostumeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyEquipmentSelection(EquipmentCostumeComboBox, static (viewModel, characterId, itemId) => viewModel.SetEquippedCostume(characterId, itemId), "Equipment.Costume");

    private void ApplyEquipmentSelection(
        ComboBox comboBox,
        Func<SaveEditorViewModel, int, ushort, SaveEditorOperationResult> apply,
        string diagnosticTarget)
    {
        if (suppressEquipmentEvents)
        {
            return;
        }

        if (!selectedEquipmentCharacterId.HasValue)
        {
            return;
        }

        if (comboBox.SelectedItem is not InventoryItemChoiceViewState selectedItem)
        {
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = apply(viewModel, selectedEquipmentCharacterId.Value, selectedItem.ItemId);
        RefreshEquipmentState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync($"{diagnosticTarget} update failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private bool TryReadInventoryQuantity(out byte quantity)
    {
        if (byte.TryParse(InventoryQuantityTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
        {
            return true;
        }

        quantity = 0;
        SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI011", "Inventory quantity must be a whole number from 0 to 255.", "Inventory.Quantity")]);
        return false;
    }

    private string GetPartyMemberValue(int slotIndex) =>
        viewModel.PartyMembers.Count > slotIndex
            ? viewModel.PartyMembers[slotIndex].MemberValue.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    private string BuildPersonaSummary()
    {
        if (!viewModel.HasSave)
        {
            return "Open a save to inspect persona slot projections.";
        }

        List<string> lines =
        [
            $"Protagonist slots: {viewModel.ProtagonistPersonaSlots.Count}",
            $"Party persona slots: {viewModel.PartyPersonaSlots.Count}",
            $"Compendium slots: {viewModel.CompendiumPersonaSlots.Count}",
            string.Empty,
            "First protagonist slots:",
        ];

        lines.AddRange(viewModel.ProtagonistPersonaSlots.Take(8).Select(FormatPersonaSlot));
        return string.Join(Environment.NewLine, lines);
    }

    private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        DiagnosticsListView.ItemsSource = diagnostics.Count == 0
            ? new[] { "No diagnostics." }
            : diagnostics.Select(FormatDiagnostic).ToArray();
    }

    private void SetUiDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        uiDiagnosticsOverride = diagnostics;
        DisplayDiagnostics(diagnostics);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private static SaveDiagnostic CreateUiDiagnostic(string code, string message, string target) =>
        new(DiagnosticSeverity.Error, code, message, target);

    private static bool IsPersistenceException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string FormatPersonaSlot(PersonaSlotViewState slot) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Slot {slot.SlotIndex}: exists={slot.Exists}, id={slot.PersonaId}, level={slot.Level}, exp={slot.TotalExperience}");

    private static string FormatDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "No diagnostics were reported."
            : string.Join(Environment.NewLine, diagnostics.Select(FormatDiagnostic));

    private static string FormatDiagnostic(SaveDiagnostic diagnostic) =>
        string.IsNullOrWhiteSpace(diagnostic.Target)
            ? string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}")
            : string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code} [{diagnostic.Target}]: {diagnostic.Message}");

    private static string FormatBoolean(bool value) =>
        value ? "yes" : "no";
}
