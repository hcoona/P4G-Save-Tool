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
    private enum BusyOperationCompletion
    {
        RefreshViewModel,
        PreserveEditorState,
    }

    private readonly SaveEditorViewModel viewModel;
    private readonly InventorySelectionState inventorySelectionState = new();
    private IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride;
    private string? currentFilePath;
    private bool isBusy;
    private bool suppressInventoryEvents;
    private bool suppressEquipmentEvents;
    private bool suppressPersonaEvents;
    private bool preserveEditorTextDuringInventoryRefresh;
    private bool preservePersonaEditorStateDuringEquipmentRefresh;
    private bool preserveSaveEditorStateDuringRefresh;
    private bool autoSelectInventoryEntryAfterOpen;
    private byte? selectedInventoryCategoryId;
    private ushort? selectedInventoryItemId;
    private ushort? selectedInventoryEntryId;
    private byte? selectedEquipmentCharacterId;
    private byte? selectedPersonaMemberId;
    private int selectedPersonaSlotIndex;

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

        if (preservePersonaEditorStateDuringEquipmentRefresh)
        {
            RefreshEquipmentState();
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return;
        }

        if (preserveSaveEditorStateDuringRefresh)
        {
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return;
        }

        RefreshFromViewModel();
    }

    private async Task RunBusyAsync(Func<Task<BusyOperationCompletion>> operation)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        UpdateShellState();
        BusyOperationCompletion completion = BusyOperationCompletion.RefreshViewModel;
        try
        {
            completion = await operation();
        }
        finally
        {
            isBusy = false;
            if (completion == BusyOperationCompletion.RefreshViewModel)
            {
                RefreshFromViewModel();
            }
            else
            {
                UpdateShellState();
            }
        }
    }

    private async Task<BusyOperationCompletion> OpenSaveFileAsync()
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
            return BusyOperationCompletion.RefreshViewModel;
        }

        if (string.IsNullOrWhiteSpace(file.Path))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI001", "The selected file does not expose a local path.", "Open")]);
            return BusyOperationCompletion.RefreshViewModel;
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
                selectedPersonaMemberId = 0;
                selectedPersonaSlotIndex = 0;
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

        return BusyOperationCompletion.RefreshViewModel;
    }

    private bool ApplyEditorFields()
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI003", "Open a save before applying edits.", "Edit")]);
            return false;
        }

        if (!TryBuildEditBatch(
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics))
        {
            SetUiDiagnostics(diagnostics);
            return false;
        }

        uiDiagnosticsOverride = null;
        preserveSaveEditorStateDuringRefresh = true;
        SaveEditorOperationResult result;
        try
        {
            result = viewModel.ApplyEdits(edits);
        }
        finally
        {
            preserveSaveEditorStateDuringRefresh = false;
        }

        if (!result.Succeeded)
        {
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return false;
        }

        RefreshFromViewModel();
        return true;
    }

    private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)
    {
        if (!ApplyEditorFields())
        {
            return BusyOperationCompletion.PreserveEditorState;
        }

        string? targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)
            ? await PickSavePathAsync()
            : currentFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return BusyOperationCompletion.RefreshViewModel;
        }

        uiDiagnosticsOverride = null;
        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        RefreshFromViewModel();
        if (!writeResult.Succeeded || writeResult.Bytes is null || writeResult.OperationToken is null)
        {
            await ShowMessageAsync("Save failed", FormatDiagnostics(writeResult.Diagnostics));
            return BusyOperationCompletion.RefreshViewModel;
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
            return BusyOperationCompletion.RefreshViewModel;
        }

        currentFilePath = targetPath;
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);
        RefreshFromViewModel();
        if (!acknowledgeResult.Succeeded)
        {
            await ShowMessageAsync("Save acknowledgement failed", FormatDiagnostics(acknowledgeResult.Diagnostics));
        }

        return BusyOperationCompletion.RefreshViewModel;
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

    private bool TryBuildEditBatch(
        out IReadOnlyList<SaveEditCommand> edits,
        out IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        List<SaveDiagnostic> validationDiagnostics = [];
        List<SaveEditCommand> batch = [];

        string familyName = FamilyNameTextBox.Text ?? string.Empty;
        string givenName = GivenNameTextBox.Text ?? string.Empty;

        if (uint.TryParse(YenTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedYen))
        {
            batch.Add(new SetYenEdit(parsedYen));
        }
        else
        {
            validationDiagnostics.Add(CreateUiDiagnostic("P4GWINUI006", "Yen must be an unsigned whole number.", "Yen"));
        }

        batch.Add(new SetSaveNamesEdit(familyName, givenName));
        AddPartyMemberValue(PartySlot0TextBox, 0, batch, validationDiagnostics);
        AddPartyMemberValue(PartySlot1TextBox, 1, batch, validationDiagnostics);
        AddPartyMemberValue(PartySlot2TextBox, 2, batch, validationDiagnostics);
        AddPersonaEdit(batch, validationDiagnostics);

        edits = validationDiagnostics.Count == 0 ? batch : [];
        diagnostics = validationDiagnostics;
        return validationDiagnostics.Count == 0;
    }

    private static void AddPartyMemberValue(
        TextBox textBox,
        int slotIndex,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (byte.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte memberValue))
        {
            edits.Add(new SetPartyMemberEdit(slotIndex, memberValue));
            return;
        }

        diagnostics.Add(CreateUiDiagnostic(
            "P4GWINUI007",
            $"Party slot {slotIndex + 1} must be a whole number from 0 to 255.",
            $"PartyMembers[{slotIndex}]"));
    }

    private void AddPersonaEdit(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        if (!selectedPersonaMemberId.HasValue)
        {
            diagnostics.Add(CreateUiDiagnostic("P4GWINUI012", "Select a persona member before applying persona edits.", "Persona.Member"));
            return;
        }

        if (selectedPersonaMemberId.Value == 0)
        {
            if (PersonaSlotComboBox.SelectedItem is not PersonaSlotViewState selectedSlot)
            {
                diagnostics.Add(CreateUiDiagnostic("P4GWINUI013", "Select a protagonist persona slot before applying edits.", "Persona.Slot"));
                return;
            }

            if (!TryBuildPersonaSlotEdit(out PersonaSlotEdit personaSlotEdit, out SaveDiagnostic diagnostic))
            {
                diagnostics.Add(diagnostic);
                return;
            }

            if (ShouldSkipPersonaEdit(selectedSlot, personaSlotEdit))
            {
                return;
            }

            edits.Add(new SetProtagonistPersonaSlotEdit(selectedSlot.SlotIndex, personaSlotEdit));
            return;
        }

        int partySlotIndex = selectedPersonaMemberId.Value - 1;
        PersonaSlotViewState currentPartySlot = viewModel.PartyPersonaSlots[partySlotIndex];
        if (!TryBuildPersonaSlotEdit(out PersonaSlotEdit partyPersonaSlotEdit, out SaveDiagnostic partyDiagnostic))
        {
            diagnostics.Add(partyDiagnostic);
            return;
        }

        if (ShouldSkipPersonaEdit(currentPartySlot, partyPersonaSlotEdit))
        {
            return;
        }

        edits.Add(new SetPartyPersonaSlotEdit(partySlotIndex, partyPersonaSlotEdit));
    }

    internal static bool ShouldSkipPersonaEdit(PersonaSlotViewState currentSlot, PersonaSlotEdit personaSlotEdit)
    {
        ArgumentNullException.ThrowIfNull(currentSlot);
        ArgumentNullException.ThrowIfNull(personaSlotEdit);

        return currentSlot.PersonaId == personaSlotEdit.PersonaId &&
            currentSlot.Level == personaSlotEdit.Level &&
            currentSlot.TotalExperience == personaSlotEdit.TotalExperience &&
            currentSlot.SkillIds.SequenceEqual(personaSlotEdit.SkillIds) &&
            currentSlot.Strength == personaSlotEdit.Strength &&
            currentSlot.Magic == personaSlotEdit.Magic &&
            currentSlot.Endurance == personaSlotEdit.Endurance &&
            currentSlot.Agility == personaSlotEdit.Agility &&
            currentSlot.Luck == personaSlotEdit.Luck;
    }

    private bool TryBuildPersonaSlotEdit(
        out PersonaSlotEdit personaSlotEdit,
        out SaveDiagnostic diagnostic)
    {
        personaSlotEdit = new PersonaSlotEdit(0, 0, 0, Array.Empty<ushort>(), 0, 0, 0, 0, 0);
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");

        if (!uint.TryParse(PersonaXpTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint totalExperience))
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI015", "Persona total experience must be an unsigned whole number.", "Persona.Xp");
            return false;
        }

        if (!TryReadSelectedPersonaId(out ushort personaId, out diagnostic) ||
            !TryReadSelectedSkillIds(out IReadOnlyList<ushort> skillIds, out diagnostic))
        {
            return false;
        }

        byte level = (byte)Math.Round(PersonaLevelSlider.Value, MidpointRounding.AwayFromZero);
        personaSlotEdit = new PersonaSlotEdit(
            personaId,
            level,
            totalExperience,
            skillIds,
            (byte)Math.Round(PersonaStrengthSlider.Value, MidpointRounding.AwayFromZero),
            (byte)Math.Round(PersonaMagicSlider.Value, MidpointRounding.AwayFromZero),
            (byte)Math.Round(PersonaEnduranceSlider.Value, MidpointRounding.AwayFromZero),
            (byte)Math.Round(PersonaAgilitySlider.Value, MidpointRounding.AwayFromZero),
            (byte)Math.Round(PersonaLuckSlider.Value, MidpointRounding.AwayFromZero));
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");
        return true;
    }

    private bool TryReadSelectedPersonaId(out ushort personaId, out SaveDiagnostic diagnostic)
    {
        if (PersonaChoiceComboBox.SelectedItem is PersonaChoiceViewState selectedPersona)
        {
            personaId = selectedPersona.PersonaId;
            diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");
            return true;
        }

        personaId = 0;
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");
        return false;
    }

    private bool TryReadSelectedSkillIds(out IReadOnlyList<ushort> skillIds, out SaveDiagnostic diagnostic)
    {
        ushort[] selectedSkillIds =
        [
            ReadSkillId(PersonaSkillBox1),
            ReadSkillId(PersonaSkillBox2),
            ReadSkillId(PersonaSkillBox3),
            ReadSkillId(PersonaSkillBox4),
            ReadSkillId(PersonaSkillBox5),
            ReadSkillId(PersonaSkillBox6),
            ReadSkillId(PersonaSkillBox7),
            ReadSkillId(PersonaSkillBox8),
        ];

        if (selectedSkillIds.Any(static skillId => skillId == ushort.MaxValue))
        {
            skillIds = Array.Empty<ushort>();
            diagnostic = CreateUiDiagnostic("P4GWINUI016", "Select a skill for each persona slot.", "Persona.Skills");
            return false;
        }

        skillIds = selectedSkillIds;
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");
        return true;
    }

    private static ushort ReadSkillId(ComboBox comboBox) =>
        comboBox.SelectedItem is SkillChoiceViewState selectedSkill ? selectedSkill.SkillId : ushort.MaxValue;

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
        RefreshEquipmentState();
        RefreshPersonaState();
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
        PersonaMemberComboBox.IsEnabled = canEdit;
        PersonaSlotComboBox.IsEnabled = canEdit && selectedPersonaMemberId == 0;
        PersonaChoiceComboBox.IsEnabled = canEdit;
        PersonaXpTextBox.IsEnabled = canEdit;
        PersonaLevelSlider.IsEnabled = canEdit;
        PersonaStrengthSlider.IsEnabled = canEdit;
        PersonaMagicSlider.IsEnabled = canEdit;
        PersonaEnduranceSlider.IsEnabled = canEdit;
        PersonaAgilitySlider.IsEnabled = canEdit;
        PersonaLuckSlider.IsEnabled = canEdit;
        PersonaSkillBox1.IsEnabled = canEdit;
        PersonaSkillBox2.IsEnabled = canEdit;
        PersonaSkillBox3.IsEnabled = canEdit;
        PersonaSkillBox4.IsEnabled = canEdit;
        PersonaSkillBox5.IsEnabled = canEdit;
        PersonaSkillBox6.IsEnabled = canEdit;
        PersonaSkillBox7.IsEnabled = canEdit;
        PersonaSkillBox8.IsEnabled = canEdit;
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

    private void RefreshPersonaState()
    {
        suppressPersonaEvents = true;
        try
        {
            PersonaMemberComboBox.ItemsSource = viewModel.HasSave
                ? viewModel.PartyMemberChoices
                : Array.Empty<PartyMemberChoiceViewState>();

            if (!viewModel.HasSave || viewModel.PartyMemberChoices.Count == 0)
            {
                selectedPersonaMemberId = null;
                selectedPersonaSlotIndex = 0;
                PersonaMemberComboBox.SelectedItem = null;
                PersonaSlotComboBox.ItemsSource = Array.Empty<PersonaSlotViewState>();
                PersonaSlotComboBox.SelectedItem = null;
                PersonaChoiceComboBox.ItemsSource = Array.Empty<PersonaChoiceViewState>();
                PersonaChoiceComboBox.SelectedItem = null;
                PersonaXpTextBox.Text = string.Empty;
                PersonaLevelSlider.Value = 0;
                PersonaStrengthSlider.Value = 0;
                PersonaMagicSlider.Value = 0;
                PersonaEnduranceSlider.Value = 0;
                PersonaAgilitySlider.Value = 0;
                PersonaLuckSlider.Value = 0;
                PersonaSkillBox1.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox1.SelectedItem = null;
                PersonaSkillBox2.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox2.SelectedItem = null;
                PersonaSkillBox3.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox3.SelectedItem = null;
                PersonaSkillBox4.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox4.SelectedItem = null;
                PersonaSkillBox5.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox5.SelectedItem = null;
                PersonaSkillBox6.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox6.SelectedItem = null;
                PersonaSkillBox7.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox7.SelectedItem = null;
                PersonaSkillBox8.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox8.SelectedItem = null;
                return;
            }

            PartyMemberChoiceViewState? selectedMember = null;
            if (selectedPersonaMemberId.HasValue)
            {
                selectedMember = viewModel.PartyMemberChoices.FirstOrDefault(
                    member => member.MemberId == selectedPersonaMemberId.Value);
            }

            selectedMember ??= viewModel.PartyMemberChoices[0];
            PersonaMemberComboBox.SelectedItem = selectedMember;
            selectedPersonaMemberId = selectedMember.MemberId;

            bool isProtagonist = selectedMember.MemberId == 0;
            IReadOnlyList<PersonaSlotViewState> personaSlots = isProtagonist
                ? viewModel.ProtagonistPersonaSlots
                : viewModel.PartyPersonaSlots;

            if (personaSlots.Count == 0)
            {
                PersonaSlotComboBox.ItemsSource = Array.Empty<PersonaSlotViewState>();
                PersonaSlotComboBox.SelectedItem = null;
                return;
            }

            if (isProtagonist)
            {
                selectedPersonaSlotIndex = Math.Clamp(selectedPersonaSlotIndex, 0, personaSlots.Count - 1);
                PersonaSlotComboBox.ItemsSource = personaSlots;
                PersonaSlotComboBox.SelectedItem = personaSlots[selectedPersonaSlotIndex];
            }
            else
            {
                int partyPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1, 0, personaSlots.Count - 1);
                PersonaSlotComboBox.ItemsSource = Array.Empty<PersonaSlotViewState>();
                PersonaSlotComboBox.SelectedItem = null;
                PersonaChoiceComboBox.ItemsSource = Array.Empty<PersonaChoiceViewState>();
                PersonaChoiceComboBox.SelectedItem = null;
                PersonaXpTextBox.Text = string.Empty;
                PersonaLevelSlider.Value = 0;
                PersonaStrengthSlider.Value = 0;
                PersonaMagicSlider.Value = 0;
                PersonaEnduranceSlider.Value = 0;
                PersonaAgilitySlider.Value = 0;
                PersonaLuckSlider.Value = 0;
                PersonaSkillBox1.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox1.SelectedItem = null;
                PersonaSkillBox2.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox2.SelectedItem = null;
                PersonaSkillBox3.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox3.SelectedItem = null;
                PersonaSkillBox4.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox4.SelectedItem = null;
                PersonaSkillBox5.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox5.SelectedItem = null;
                PersonaSkillBox6.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox6.SelectedItem = null;
                PersonaSkillBox7.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox7.SelectedItem = null;
                PersonaSkillBox8.ItemsSource = Array.Empty<SkillChoiceViewState>();
                PersonaSkillBox8.SelectedItem = null;
                PersonaSlotViewState partyCurrentSlot = personaSlots[partyPersonaSlotIndex];
                PersonaChoiceComboBox.ItemsSource = viewModel.GetPersonaChoices(
                    partyCurrentSlot.PersonaId,
                    out PersonaChoiceViewState partySelectedPersonaChoice);
                PersonaChoiceComboBox.SelectedItem = partySelectedPersonaChoice;
                PersonaXpTextBox.Text = partyCurrentSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
                PersonaLevelSlider.Value = partyCurrentSlot.Level;
                PersonaStrengthSlider.Value = partyCurrentSlot.Strength;
                PersonaMagicSlider.Value = partyCurrentSlot.Magic;
                PersonaEnduranceSlider.Value = partyCurrentSlot.Endurance;
                PersonaAgilitySlider.Value = partyCurrentSlot.Agility;
                PersonaLuckSlider.Value = partyCurrentSlot.Luck;
                SetPersonaSkillChoices(partyCurrentSlot.SkillIds);
                UpdateShellState();
                return;
            }

            PersonaSlotViewState currentSlot = personaSlots[selectedPersonaSlotIndex];
            PersonaChoiceComboBox.ItemsSource = viewModel.GetPersonaChoices(currentSlot.PersonaId, out PersonaChoiceViewState selectedPersonaChoice);
            PersonaChoiceComboBox.SelectedItem = selectedPersonaChoice;
            PersonaXpTextBox.Text = currentSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
            PersonaLevelSlider.Value = currentSlot.Level;
            PersonaStrengthSlider.Value = currentSlot.Strength;
            PersonaMagicSlider.Value = currentSlot.Magic;
            PersonaEnduranceSlider.Value = currentSlot.Endurance;
            PersonaAgilitySlider.Value = currentSlot.Agility;
            PersonaLuckSlider.Value = currentSlot.Luck;

            SetPersonaSkillChoices(currentSlot.SkillIds);
        }
        finally
        {
            suppressPersonaEvents = false;
            RefreshPersonaSummary();
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

    private void SetPersonaSkillChoices(IReadOnlyList<ushort> skillIds)
    {
        IReadOnlyList<SkillChoiceViewState> skill1Choices = viewModel.GetSkillChoices(skillIds[0], out SkillChoiceViewState skill1);
        IReadOnlyList<SkillChoiceViewState> skill2Choices = viewModel.GetSkillChoices(skillIds[1], out SkillChoiceViewState skill2);
        IReadOnlyList<SkillChoiceViewState> skill3Choices = viewModel.GetSkillChoices(skillIds[2], out SkillChoiceViewState skill3);
        IReadOnlyList<SkillChoiceViewState> skill4Choices = viewModel.GetSkillChoices(skillIds[3], out SkillChoiceViewState skill4);
        IReadOnlyList<SkillChoiceViewState> skill5Choices = viewModel.GetSkillChoices(skillIds[4], out SkillChoiceViewState skill5);
        IReadOnlyList<SkillChoiceViewState> skill6Choices = viewModel.GetSkillChoices(skillIds[5], out SkillChoiceViewState skill6);
        IReadOnlyList<SkillChoiceViewState> skill7Choices = viewModel.GetSkillChoices(skillIds[6], out SkillChoiceViewState skill7);
        IReadOnlyList<SkillChoiceViewState> skill8Choices = viewModel.GetSkillChoices(skillIds[7], out SkillChoiceViewState skill8);

        PersonaSkillBox1.ItemsSource = skill1Choices;
        PersonaSkillBox1.SelectedItem = skill1;
        PersonaSkillBox2.ItemsSource = skill2Choices;
        PersonaSkillBox2.SelectedItem = skill2;
        PersonaSkillBox3.ItemsSource = skill3Choices;
        PersonaSkillBox3.SelectedItem = skill3;
        PersonaSkillBox4.ItemsSource = skill4Choices;
        PersonaSkillBox4.SelectedItem = skill4;
        PersonaSkillBox5.ItemsSource = skill5Choices;
        PersonaSkillBox5.SelectedItem = skill5;
        PersonaSkillBox6.ItemsSource = skill6Choices;
        PersonaSkillBox6.SelectedItem = skill6;
        PersonaSkillBox7.ItemsSource = skill7Choices;
        PersonaSkillBox7.SelectedItem = skill7;
        PersonaSkillBox8.ItemsSource = skill8Choices;
        PersonaSkillBox8.SelectedItem = skill8;
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

    private void PersonaMemberComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressPersonaEvents)
        {
            return;
        }

        if (PersonaMemberComboBox.SelectedItem is not PartyMemberChoiceViewState selectedMember)
        {
            selectedPersonaMemberId = null;
            RefreshPersonaSummary();
            UpdateShellState();
            return;
        }

        selectedPersonaMemberId = selectedMember.MemberId;

        RefreshPersonaState();
        UpdateShellState();
    }

    private void PersonaSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressPersonaEvents)
        {
            return;
        }

        if (PersonaSlotComboBox.SelectedItem is PersonaSlotViewState selectedSlot)
        {
            selectedPersonaSlotIndex = selectedSlot.SlotIndex;
            RefreshPersonaState();
            UpdateShellState();
        }
    }

    private void PersonaChoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressPersonaEvents)
        {
            return;
        }

        UpdateShellState();
    }

    private void PersonaSkillBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressPersonaEvents)
        {
            return;
        }

        UpdateShellState();
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
        preservePersonaEditorStateDuringEquipmentRefresh = true;
        SaveEditorOperationResult result;
        try
        {
            result = apply(viewModel, selectedEquipmentCharacterId.Value, selectedItem.ItemId);
        }
        finally
        {
            preservePersonaEditorStateDuringEquipmentRefresh = false;
        }

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

        PartyMemberChoiceViewState? selectedMember = selectedPersonaMemberId.HasValue
            ? viewModel.PartyMemberChoices.FirstOrDefault(member => member.MemberId == selectedPersonaMemberId.Value)
            : null;
        if (selectedMember is null)
        {
            return "Select a persona member to inspect the active persona slot.";
        }

        IReadOnlyList<PersonaSlotViewState> personaSlots = selectedMember.MemberId == 0
            ? viewModel.ProtagonistPersonaSlots
            : viewModel.PartyPersonaSlots;
        if (personaSlots.Count == 0)
        {
            return $"{selectedMember.Name}: no persona slots are available.";
        }

        int slotIndex = selectedMember.MemberId == 0
            ? Math.Clamp(selectedPersonaSlotIndex, 0, personaSlots.Count - 1)
            : Math.Clamp(selectedMember.MemberId - 1, 0, personaSlots.Count - 1);
        PersonaSlotViewState slot = personaSlots[slotIndex];
        viewModel.GetPersonaChoices(slot.PersonaId, out PersonaChoiceViewState selectedPersona);

        return string.Join(
            Environment.NewLine,
            [
                $"Member: {selectedMember.Name}",
                $"Slot: {slotIndex}",
                $"Persona: {selectedPersona.Name}",
                $"Level: {slot.Level}",
                $"XP: {slot.TotalExperience}",
            ]);
    }

    private void RefreshPersonaSummary()
    {
        PersonaSummaryTextBox.Text = BuildPersonaSummary();
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
