using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using P4G.SaveTool.Application;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace P4G.SaveTool.WinUI;

public sealed partial class MainWindow : Window
{
    private const uint LegacyCompendiumMaximumTotalExperience = 999_999_999;

    private enum BusyOperationCompletion
    {
        RefreshViewModel,
        PreserveEditorState,
    }

    private readonly SaveEditorViewModel viewModel;
    private readonly ObservableCollection<string> diagnosticsItems = new();
    private readonly ObservableCollection<SocialLinkViewState> socialLinkItems = new();
    private readonly ObservableCollection<SocialLinkChoiceViewState> socialLinkChoices = new();
    private readonly ObservableCollection<CompendiumPersonaViewState> compendiumItems = new();
    private readonly ObservableCollection<PersonaChoiceViewState> compendiumAddChoices = new();
    private readonly ObservableCollection<InventoryStackViewState> inventoryItems = new();
    private readonly ObservableCollection<ItemCategoryViewState> inventoryCategories = new();
    private readonly ObservableCollection<InventoryItemChoiceViewState> inventoryItemChoices = new();
    private readonly ObservableCollection<EquipmentCharacterViewState> equipmentCharacters = new();
    private readonly ObservableCollection<InventoryItemChoiceViewState> equipmentWeaponChoices = new();
    private readonly ObservableCollection<InventoryItemChoiceViewState> equipmentArmorChoices = new();
    private readonly ObservableCollection<InventoryItemChoiceViewState> equipmentAccessoryChoices = new();
    private readonly ObservableCollection<InventoryItemChoiceViewState> equipmentCostumeChoices = new();
    private readonly ObservableCollection<PartyConfigurationChoiceViewState> partySlot0Choices = new();
    private readonly ObservableCollection<PartyConfigurationChoiceViewState> partySlot1Choices = new();
    private readonly ObservableCollection<PartyConfigurationChoiceViewState> partySlot2Choices = new();
    private readonly ObservableCollection<PartyMemberChoiceViewState> personaMemberChoices = new();
    private readonly ObservableCollection<PersonaSlotViewState> personaSlotChoices = new();
    private readonly ObservableCollection<PersonaChoiceViewState> personaChoices = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices1 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices2 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices3 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices4 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices5 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices6 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices7 = new();
    private readonly ObservableCollection<SkillChoiceViewState> personaSkillChoices8 = new();
    private readonly InventorySelectionState inventorySelectionState = new();
    private readonly SaveEditorRefreshCoordinator saveEditorRefreshCoordinator = new();
    private readonly Brush? defaultMainCharacterLevelValueForeground;
    private readonly Brush? defaultPersonaLevelValueForeground;
    private readonly SolidColorBrush legacyLevelWarningForeground = new(Colors.Red);
    private string? startupOpenPath;
    private IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride;
    private string? currentFilePath;
    private bool isBusy;
    private bool suppressInventoryEvents;
    private bool suppressEquipmentEvents;
    private bool suppressPersonaEvents;
    private bool suppressCompendiumEvents;
    private bool suppressSocialLinkEvents;
    private bool suppressImmediateEditEvents;
    private bool preserveEditorTextDuringInventoryRefresh;
    private bool preservePersonaEditorStateDuringEquipmentRefresh;
    private bool autoSelectInventoryEntryAfterOpen;
    private bool autoSelectCompendiumEntryAfterOpen;
    private bool refreshEditableFieldsAfterStartupOpen;
    private bool inventoryQuantityDraftDirty;
    private byte? selectedInventoryCategoryId;
    private ushort? selectedInventoryItemId;
    private ushort? selectedInventoryEntryId;
    private byte? selectedEquipmentCharacterId;
    private int? selectedCompendiumSlotIndex;
    private int? selectedSocialLinkIndex;
    private byte? selectedSocialLinkLinkId;
    private byte? selectedPersonaMemberId;
    private int selectedPersonaSlotIndex;

    public MainWindow(string? startupOpenPath = null)
    {
        this.startupOpenPath = startupOpenPath;
        InitializeComponent();
        DiagnosticsListView.ItemsSource = diagnosticsItems;
        SocialLinkListView.ItemsSource = socialLinkItems;
        SocialLinkAddComboBox.ItemsSource = socialLinkChoices;
        CompendiumListView.ItemsSource = compendiumItems;
        CompendiumAddComboBox.ItemsSource = compendiumAddChoices;
        InventoryListView.ItemsSource = inventoryItems;
        InventoryCategoryComboBox.ItemsSource = inventoryCategories;
        InventoryItemComboBox.ItemsSource = inventoryItemChoices;
        EquipmentCharacterComboBox.ItemsSource = equipmentCharacters;
        EquipmentWeaponComboBox.ItemsSource = equipmentWeaponChoices;
        EquipmentArmorComboBox.ItemsSource = equipmentArmorChoices;
        EquipmentAccessoryComboBox.ItemsSource = equipmentAccessoryChoices;
        EquipmentCostumeComboBox.ItemsSource = equipmentCostumeChoices;
        PartySlot0ComboBox.ItemsSource = partySlot0Choices;
        PartySlot1ComboBox.ItemsSource = partySlot1Choices;
        PartySlot2ComboBox.ItemsSource = partySlot2Choices;
        PersonaMemberComboBox.ItemsSource = personaMemberChoices;
        PersonaSlotComboBox.ItemsSource = personaSlotChoices;
        PersonaChoiceComboBox.ItemsSource = personaChoices;
        PersonaSkillBox1.ItemsSource = personaSkillChoices1;
        PersonaSkillBox2.ItemsSource = personaSkillChoices2;
        PersonaSkillBox3.ItemsSource = personaSkillChoices3;
        PersonaSkillBox4.ItemsSource = personaSkillChoices4;
        PersonaSkillBox5.ItemsSource = personaSkillChoices5;
        PersonaSkillBox6.ItemsSource = personaSkillChoices6;
        PersonaSkillBox7.ItemsSource = personaSkillChoices7;
        PersonaSkillBox8.ItemsSource = personaSkillChoices8;

        viewModel = new SaveEditorViewModel(new SaveApplicationService());
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        defaultMainCharacterLevelValueForeground = MainCharacterLevelValueTextBlock.Foreground;
        defaultPersonaLevelValueForeground = PersonaLevelValueTextBlock.Foreground;
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

    private async void About_Click(object sender, RoutedEventArgs e) =>
        await ShowAboutDialogAsync();

    private void MainCharacterLevelSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateMainCharacterLevelValueText();
        ApplyImmediateEdit(
            () => new SetMainCharacterLevelEdit((byte)MainCharacterLevelSlider.Value),
            refreshAfterSuccess: false);
    }

    private void JumpBasicStats_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(BasicStatsSectionHeader);

    private void JumpCalendarSocialStats_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(CalendarSocialStatsSectionHeader);

    private void JumpSocialLinks_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(SocialLinksSectionHeader);

    private void JumpPartyPersona_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(PartyPersonaSectionHeader);

    private void JumpEquipment_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(EquipmentSectionHeader);

    private void JumpCompendium_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(CompendiumSectionHeader);

    private void JumpInventory_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(InventorySectionHeader);

    private void JumpDiagnosticsState_Click(object sender, RoutedEventArgs e) =>
        NavigateToSection(DiagnosticsStateSectionHeader);

    private void PersonaCalculateFromLevelButton_Click(object sender, RoutedEventArgs e) =>
        PersonaXpTextBox.Text = LevelExperienceProjection.CalculateTotalExperienceFromLevel((byte)PersonaLevelSlider.Value).ToString(CultureInfo.InvariantCulture);

    private void PersonaLevelSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        RefreshPersonaDraftShellState(UpdatePersonaLevelValueText);

    private void PersonaDraftControl_Changed(object sender, TextChangedEventArgs e) =>
        RefreshPersonaDraftShellState();

    private void PersonaDraftControl_Changed(object sender, RangeBaseValueChangedEventArgs e) =>
        RefreshPersonaDraftShellState();

    private void RefreshPersonaDraftShellState(Action? refreshValueText = null)
    {
        refreshValueText?.Invoke();
        if (suppressPersonaEvents || viewModel is null || !viewModel.HasSave)
        {
            return;
        }

        RefreshPersonaDraftDiagnostics();
        UpdateShellState();
    }

    private void MainCharacterCalculateFromLevelButton_Click(object sender, RoutedEventArgs e) =>
        MainCharacterTotalExperienceTextBox.Text = LevelExperienceProjection.CalculateTotalExperienceFromLevel((byte)MainCharacterLevelSlider.Value).ToString(CultureInfo.InvariantCulture);

    private void UpdateMainCharacterLevelValueText() =>
        UpdateLevelValueText(
            MainCharacterLevelValueTextBlock,
            MainCharacterLevelSlider.Value,
            defaultMainCharacterLevelValueForeground);

    private void UpdatePersonaLevelValueText() =>
        UpdateLevelValueText(
            PersonaLevelValueTextBlock,
            PersonaLevelSlider.Value,
            defaultPersonaLevelValueForeground);

    private void UpdateLevelValueText(TextBlock valueTextBlock, double level, Brush? defaultForeground)
    {
        bool hasSave = viewModel is not null && viewModel.HasSave;
        valueTextBlock.Text = hasSave
            ? ((byte)Math.Round(level, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        valueTextBlock.Foreground = hasSave && IsLegacyLevelWarningValue(level)
            ? legacyLevelWarningForeground
            : defaultForeground;
    }

    internal static bool IsLegacyLevelWarningValue(double level) =>
        Math.Round(level, MidpointRounding.AwayFromZero) > 99;

    private static void SetLevelSliderValue(Slider slider, double rawLevel) =>
        slider.Value = Math.Clamp(rawLevel, slider.Minimum, slider.Maximum);

    internal readonly record struct SocialLinkDraftState(
        int SlotIndex,
        byte LinkId,
        string LevelText,
        string ProgressText);

    internal readonly record struct CompendiumDraftState(
        int SlotIndex,
        ushort PersonaId,
        string ExperienceText,
        double Level,
        double Strength,
        double Magic,
        double Endurance,
        double Agility,
        double Luck,
        ushort Skill1Id,
        ushort Skill2Id,
        ushort Skill3Id,
        ushort Skill4Id,
        ushort Skill5Id,
        ushort Skill6Id,
        ushort Skill7Id,
        ushort Skill8Id);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isBusy)
        {
            return;
        }

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

        if (saveEditorRefreshCoordinator.IsFullRefreshSuppressed)
        {
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return;
        }

        RefreshFromViewModel();
    }

    private async Task RunBusyAsync(Func<Task<BusyOperationCompletion>> operation)
    {
        if (!TryBeginBusyOperation(ref isBusy))
        {
            return;
        }

        UpdateShellState();
        BusyOperationCompletion completion = BusyOperationCompletion.RefreshViewModel;
        try
        {
            completion = await operation();
        }
        finally
        {
            EndBusyOperation(ref isBusy);
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
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return BusyOperationCompletion.PreserveEditorState;
        }

        if (string.IsNullOrWhiteSpace(file.Path))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI001", "The selected file does not expose a local path.", "Open")]);
            return BusyOperationCompletion.PreserveEditorState;
        }

        return await OpenSaveFileFromPathAsync(file.Path, "Open");
    }

    private async Task<BusyOperationCompletion> OpenSaveFileFromPathAsync(string path, string source)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI001", "The selected file does not expose a local path.", source)]);
            return BusyOperationCompletion.PreserveEditorState;
        }

        if (!ShellDragDropHelper.TryGetOpenablePath(path, out string openablePath))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI029", "Select a Persona 4 Golden .bin save file.", source)]);
            await ShowMessageAsync("Open failed", "Select a Persona 4 Golden .bin save file.");
            return BusyOperationCompletion.PreserveEditorState;
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(openablePath);
            uiDiagnosticsOverride = null;
            SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.OpenSave(bytes));
            ApplyOpenResult(
                result.Succeeded,
                () =>
                {
                    currentFilePath = openablePath;
                    selectedInventoryCategoryId = null;
                    selectedInventoryItemId = null;
                    selectedInventoryEntryId = null;
                    selectedEquipmentCharacterId = null;
                    selectedCompendiumSlotIndex = null;
                    selectedSocialLinkIndex = null;
                    selectedSocialLinkLinkId = null;
                    selectedPersonaMemberId = 0;
                    selectedPersonaSlotIndex = 0;
                    inventoryQuantityDraftDirty = false;
                    inventorySelectionState.Reset();
                    autoSelectInventoryEntryAfterOpen = true;
                    autoSelectCompendiumEntryAfterOpen = true;
                    InventoryQuantityTextBox.Text = string.Empty;
                    UpdateShellState();
                },
                UpdateShellState);

            if (!result.Succeeded)
            {
                DisplayDiagnostics(result.Diagnostics);
                await ShowMessageAsync("Open failed", FormatDiagnostics(result.Diagnostics));
                refreshEditableFieldsAfterStartupOpen = false;
                return BusyOperationCompletion.PreserveEditorState;
            }

            if (string.Equals(source, "Launch", StringComparison.Ordinal))
            {
                refreshEditableFieldsAfterStartupOpen = true;
                return BusyOperationCompletion.PreserveEditorState;
            }

            return BusyOperationCompletion.RefreshViewModel;
        }
        catch (Exception ex) when (IsPersistenceException(ex))
        {
            refreshEditableFieldsAfterStartupOpen = false;
            await ReportOpenFailureAsync(source, $"Could not read the selected file: {ex.Message}");
        }
        catch (Exception ex)
        {
            refreshEditableFieldsAfterStartupOpen = false;
            await ReportOpenFailureAsync(source, $"Could not open the selected file: {ex.Message}");
        }

        return BusyOperationCompletion.PreserveEditorState;
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
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.ApplyEdits(edits));

        if (!result.Succeeded)
        {
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return false;
        }

        RefreshFromViewModelPreservingInventoryQuantityDraft(
            preserveSelectedInventoryQuantityDraft: ShouldPreserveSelectedInventoryQuantityDraftAfterApply(edits),
            preserveSelectedSocialLinkDraft: ShouldPreserveSelectedSocialLinkDraftAfterApply(edits),
            preserveSelectedCompendiumDraft: ShouldPreserveSelectedCompendiumDraftAfterApply(edits));
        return true;
    }

    private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)
    {
        string? targetPath;
        targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)
            ? await PickSavePathAsync()
            : currentFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return BusyOperationCompletion.PreserveEditorState;
        }

        if (!ApplyEditorFields())
        {
            return BusyOperationCompletion.PreserveEditorState;
        }

        uiDiagnosticsOverride = null;
        SaveEditorWriteResult writeResult = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.WriteSave());
        if (!writeResult.Succeeded || writeResult.Bytes is null || writeResult.OperationToken is null)
        {
            RefreshFromViewModelPreservingInventoryQuantityDraft();

            await ShowMessageAsync("Save failed", FormatDiagnostics(writeResult.Diagnostics));
            return BusyOperationCompletion.PreserveEditorState;
        }

        RefreshFromViewModelPreservingInventoryQuantityDraft();

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
            SaveEditorOperationResult reportResult = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.ReportSaveFailed(operationToken, diagnostics));
            RefreshFromViewModelPreservingInventoryQuantityDraft();

            await ShowMessageAsync("Save failed", FormatDiagnostics(reportResult.Diagnostics));
            return BusyOperationCompletion.PreserveEditorState;
        }

        currentFilePath = targetPath;
        UpdateWindowTitle();
        SaveEditorOperationResult acknowledgeResult = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.AcknowledgeSaved(operationToken));
        RefreshFromViewModelPreservingInventoryQuantityDraft();
        if (!acknowledgeResult.Succeeded)
        {
            await ShowMessageAsync("Save acknowledgement failed", FormatDiagnostics(acknowledgeResult.Diagnostics));
        }

        return BusyOperationCompletion.PreserveEditorState;
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TraceStartup("MainWindow_Loaded enter");
        UpdateWindowTitle();
        if (string.IsNullOrWhiteSpace(startupOpenPath))
        {
            _ = DispatcherQueue.TryEnqueue(RefreshBasicFieldsFromViewModel);
            return;
        }

        await RunBusyAsync(
            async () =>
            {
                string? startupOpenPath = ConsumeStartupOpenPath(ref this.startupOpenPath);
                if (startupOpenPath is null)
                {
                    return BusyOperationCompletion.PreserveEditorState;
                }

                return await OpenSaveFileFromPathAsync(startupOpenPath, "Launch");
            });

        if (refreshEditableFieldsAfterStartupOpen)
        {
            RefreshEditableFields();
        }
    }

    private async void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        if (isBusy)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);
            if (isBusy)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            e.AcceptedOperation = acceptedOperation;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            if (isBusy)
            {
                return;
            }

            if (!ShellDragDropHelper.TryGetOpenablePath(items.OfType<StorageFile>().Select(file => file.Path), out string openPath))
            {
                return;
            }

            await RunBusyAsync(() => OpenSaveFileFromPathAsync(openPath, "Drop"));
        }
        catch (Exception ex)
        {
            await ReportOpenFailureAsync("Drop", $"Could not read the dropped file: {ex.Message}");
        }
    }

    private static async Task<DataPackageOperation> EvaluateDragOverAcceptanceAsync(DataPackageView dataView)
    {
        try
        {
            IReadOnlyList<IStorageItem> items = await dataView.GetStorageItemsAsync();
            return ShellDragDropHelper.GetAcceptedDragOperation(items.OfType<StorageFile>().Select(file => file.Path));
        }
        catch
        {
            return DataPackageOperation.None;
        }
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
        batch.Add(new SetMainCharacterLevelEdit((byte)MainCharacterLevelSlider.Value));
        if (uint.TryParse(MainCharacterTotalExperienceTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedMainCharacterTotalExperience))
        {
            batch.Add(new SetMainCharacterTotalExperienceEdit(parsedMainCharacterTotalExperience));
        }
        else
        {
            validationDiagnostics.Add(CreateUiDiagnostic(
                "P4GWINUI028",
                "Main character total experience must be an unsigned whole number.",
                "MainCharacter.TotalExperience"));
        }
        AddPartyMemberValue(PartySlot0ComboBox, 0, batch, validationDiagnostics);
        AddPartyMemberValue(PartySlot1ComboBox, 1, batch, validationDiagnostics);
        AddPartyMemberValue(PartySlot2ComboBox, 2, batch, validationDiagnostics);
        AppendGroup4Edits(
            viewModel.SocialStats,
            viewModel.Calendar,
            CreateGroup4EditInputs(
                CourageComboBox.SelectedItem as SocialStatRankChoiceViewState,
                KnowledgeComboBox.SelectedItem as SocialStatRankChoiceViewState,
                ExpressionComboBox.SelectedItem as SocialStatRankChoiceViewState,
                UnderstandingComboBox.SelectedItem as SocialStatRankChoiceViewState,
                DiligenceComboBox.SelectedItem as SocialStatRankChoiceViewState,
                DayTextBox.Text ?? string.Empty,
                PhaseComboBox.SelectedItem as CalendarPhaseChoiceViewState,
                NextDayTextBox.Text ?? string.Empty,
                NextPhaseComboBox.SelectedItem as CalendarPhaseChoiceViewState),
            batch,
            validationDiagnostics);
        AddPersonaEdit(batch, validationDiagnostics);
        TryAppendSelectedSocialLinkEdits(batch, validationDiagnostics);
        TryAppendSelectedInventoryQuantityEdit(
            inventoryQuantityDraftDirty,
            selectedInventoryItemId,
            InventoryQuantityTextBox.Text ?? string.Empty,
            batch,
            validationDiagnostics);

        return TryFinalizeEditBatch(batch, validationDiagnostics, out edits, out diagnostics);
    }

    private static void AddPartyMemberValue(
        ComboBox comboBox,
        int slotIndex,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (comboBox.SelectedItem is PartyConfigurationChoiceViewState selectedMember)
        {
            edits.Add(new SetPartyMemberEdit(slotIndex, selectedMember.MemberValue));
            return;
        }

        diagnostics.Add(CreateUiDiagnostic(
            "P4GWINUI007",
            $"Select a party member for slot {slotIndex + 1}.",
            $"PartyMembers[{slotIndex}]"));
    }

    private void AddPersonaEdit(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        if (selectedCompendiumSlotIndex.HasValue)
        {
            if (!TryBuildPersonaSlotEdit(out PersonaSlotEdit compendiumPersonaSlotEdit, out SaveDiagnostic compendiumDiagnostic))
            {
                diagnostics.Add(compendiumDiagnostic);
                return;
            }

            PersonaSlotViewState currentCompendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
            compendiumPersonaSlotEdit = PreserveCompendiumPersonaIdentity(currentCompendiumSlot, compendiumPersonaSlotEdit);
            if (!TryValidateCompendiumPersonaExperienceChange(currentCompendiumSlot, compendiumPersonaSlotEdit, out SaveDiagnostic experienceDiagnostic))
            {
                diagnostics.Add(experienceDiagnostic);
                return;
            }

            if (ShouldSkipPersonaEdit(currentCompendiumSlot, compendiumPersonaSlotEdit))
            {
                return;
            }

            edits.Add(new SetCompendiumPersonaSlotEdit(selectedCompendiumSlotIndex.Value, compendiumPersonaSlotEdit));
            return;
        }

        if (!selectedPersonaMemberId.HasValue)
        {
            diagnostics.Add(CreateUiDiagnostic("P4GWINUI012", "Select a persona member before applying persona edits.", "Persona.Member"));
            return;
        }

        if (selectedPersonaMemberId.Value == 0)
        {
            if ((uint)selectedPersonaSlotIndex >= (uint)viewModel.ProtagonistPersonaSlots.Count)
            {
                diagnostics.Add(CreateUiDiagnostic("P4GWINUI013", "Select a protagonist persona slot before applying edits.", "Persona.Slot"));
                return;
            }

            PersonaSlotViewState selectedSlot = viewModel.ProtagonistPersonaSlots[selectedPersonaSlotIndex];
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

    private bool TryAppendSelectedSocialLinkEdits(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return TryBuildSocialLinkEdits(
            selectedSocialLinkIndex,
            SocialLinkLevelTextBox.Text ?? string.Empty,
            SocialLinkProgressTextBox.Text ?? string.Empty,
            edits,
            diagnostics);
    }

    private static bool TryValidateCompendiumPersonaExperienceChange(
        PersonaSlotViewState currentSlot,
        PersonaSlotEdit personaSlotEdit,
        out SaveDiagnostic diagnostic)
    {
        if (personaSlotEdit.TotalExperience != currentSlot.TotalExperience &&
            personaSlotEdit.TotalExperience > LegacyCompendiumMaximumTotalExperience)
        {
            diagnostic = CreateUiDiagnostic(
                "P4GWINUI031",
                $"Compendium persona total experience must be at most {LegacyCompendiumMaximumTotalExperience.ToString(CultureInfo.InvariantCulture)}.",
                "Persona.Xp");
            return false;
        }

        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");
        return true;
    }

    internal static SocialLinkViewState? ResolveSelectedSocialLinkViewState(
        IReadOnlyList<SocialLinkViewState> socialLinks,
        int? selectedSocialLinkIndex,
        byte? selectedSocialLinkLinkId,
        bool allowFallbackSelection = false)
    {
        ArgumentNullException.ThrowIfNull(socialLinks);

        if (socialLinks.Count == 0)
        {
            return null;
        }

        if (selectedSocialLinkIndex.HasValue)
        {
            SocialLinkViewState? selectedLink = socialLinks.FirstOrDefault(link => link.SlotIndex == selectedSocialLinkIndex.Value);
            if (selectedLink is not null)
            {
                return selectedLink;
            }
        }

        if (selectedSocialLinkLinkId.HasValue)
        {
            SocialLinkViewState? selectedLink = socialLinks.FirstOrDefault(link => link.LinkId == selectedSocialLinkLinkId.Value);
            if (selectedLink is not null)
            {
                return selectedLink;
            }
        }

        return allowFallbackSelection ? socialLinks[0] : null;
    }

    internal static void ResetSelectedSocialLinkState(ref int? selectedSocialLinkIndex, ref byte? selectedSocialLinkLinkId)
    {
        selectedSocialLinkIndex = null;
        selectedSocialLinkLinkId = null;
    }

    internal static int ResolveSelectedSocialLinkRowIndex(
        IReadOnlyList<SocialLinkViewState> socialLinkRows,
        SocialLinkViewState selectedLink)
    {
        ArgumentNullException.ThrowIfNull(socialLinkRows);
        ArgumentNullException.ThrowIfNull(selectedLink);

        for (int index = 0; index < socialLinkRows.Count; index++)
        {
            SocialLinkViewState socialLinkRow = socialLinkRows[index];
            if (socialLinkRow.SlotIndex == selectedLink.SlotIndex &&
                socialLinkRow.LinkId == selectedLink.LinkId)
            {
                return index;
            }
        }

        return -1;
    }

    private SocialLinkDraftState? CaptureSelectedSocialLinkDraft()
    {
        if (!selectedSocialLinkIndex.HasValue || SocialLinkListView.SelectedItem is not SocialLinkViewState selectedLink)
        {
            return null;
        }

        return new SocialLinkDraftState(
            selectedLink.SlotIndex,
            selectedLink.LinkId,
            SocialLinkLevelTextBox.Text ?? string.Empty,
            SocialLinkProgressTextBox.Text ?? string.Empty);
    }

    private SocialLinkViewState? GetSelectedSocialLinkViewState() =>
        ResolveSelectedSocialLinkViewState(viewModel.SocialLinks, selectedSocialLinkIndex, selectedSocialLinkLinkId);

    private void RefreshSelectedSocialLinkRowSummary()
    {
        if (!selectedSocialLinkIndex.HasValue)
        {
            return;
        }

        SocialLinkViewState? selectedLink = GetSelectedSocialLinkViewState();
        if (selectedLink is null)
        {
            return;
        }

        int rowIndex = ResolveSelectedSocialLinkRowIndex(socialLinkItems, selectedLink);
        if (rowIndex < 0)
        {
            return;
        }

        suppressSocialLinkEvents = true;
        try
        {
            socialLinkItems[rowIndex] = selectedLink;
            SocialLinkListView.SelectedItem = selectedLink;
        }
        finally
        {
            suppressSocialLinkEvents = false;
        }

        selectedSocialLinkIndex = selectedLink.SlotIndex;
        selectedSocialLinkLinkId = selectedLink.LinkId;
    }

    private void RestoreSelectedSocialLinkDraft(SocialLinkDraftState socialLinkDraft)
    {
        if (!ShouldRestoreSelectedSocialLinkDraft(socialLinkDraft, GetSelectedSocialLinkViewState()))
        {
            return;
        }

        SocialLinkLevelTextBox.Text = socialLinkDraft.LevelText;
        SocialLinkProgressTextBox.Text = socialLinkDraft.ProgressText;
    }

    internal static bool ShouldRestoreSelectedSocialLinkDraft(
        SocialLinkDraftState socialLinkDraft,
        SocialLinkViewState? selectedLink) =>
        selectedLink is not null &&
        selectedLink.SlotIndex == socialLinkDraft.SlotIndex &&
        selectedLink.LinkId == socialLinkDraft.LinkId;

    private CompendiumDraftState? CaptureSelectedCompendiumDraft()
    {
        if (!selectedCompendiumSlotIndex.HasValue)
        {
            return null;
        }

        ushort selectedPersonaId = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value].PersonaId;

        return new CompendiumDraftState(
            selectedCompendiumSlotIndex.Value,
            selectedPersonaId,
            PersonaXpTextBox.Text ?? string.Empty,
            PersonaLevelSlider.Value,
            PersonaStrengthSlider.Value,
            PersonaMagicSlider.Value,
            PersonaEnduranceSlider.Value,
            PersonaAgilitySlider.Value,
            PersonaLuckSlider.Value,
            ReadSkillId(PersonaSkillBox1),
            ReadSkillId(PersonaSkillBox2),
            ReadSkillId(PersonaSkillBox3),
            ReadSkillId(PersonaSkillBox4),
            ReadSkillId(PersonaSkillBox5),
            ReadSkillId(PersonaSkillBox6),
            ReadSkillId(PersonaSkillBox7),
            ReadSkillId(PersonaSkillBox8));
    }

    private void RestoreSelectedCompendiumDraft(CompendiumDraftState compendiumDraft)
    {
        if (!ShouldRestoreSelectedCompendiumDraft(compendiumDraft, selectedCompendiumSlotIndex))
        {
            return;
        }

        suppressPersonaEvents = true;
        try
        {
            selectedCompendiumSlotIndex = compendiumDraft.SlotIndex;
            personaChoices.Clear();
            PersonaChoiceViewState selectedCompendiumChoice = default!;
            foreach (PersonaChoiceViewState choice in SaveEditorViewModel.GetPersonaChoices(
                compendiumDraft.PersonaId,
                out selectedCompendiumChoice))
            {
                personaChoices.Add(choice);
            }

            PersonaChoiceComboBox.SelectedItem = selectedCompendiumChoice;
            PersonaXpTextBox.Text = compendiumDraft.ExperienceText;
            SetLevelSliderValue(PersonaLevelSlider, compendiumDraft.Level);
            PersonaStrengthSlider.Value = compendiumDraft.Strength;
            PersonaMagicSlider.Value = compendiumDraft.Magic;
            PersonaEnduranceSlider.Value = compendiumDraft.Endurance;
            PersonaAgilitySlider.Value = compendiumDraft.Agility;
            PersonaLuckSlider.Value = compendiumDraft.Luck;
            SetPersonaSkillChoices(
                [compendiumDraft.Skill1Id,
                compendiumDraft.Skill2Id,
                compendiumDraft.Skill3Id,
                compendiumDraft.Skill4Id,
                compendiumDraft.Skill5Id,
                compendiumDraft.Skill6Id,
                compendiumDraft.Skill7Id,
                compendiumDraft.Skill8Id]);
        }
        finally
        {
            suppressPersonaEvents = false;
        }
    }

    internal static bool ShouldRestoreSelectedCompendiumDraft(
        CompendiumDraftState compendiumDraft,
        int? selectedCompendiumSlotIndex) =>
        selectedCompendiumSlotIndex.HasValue &&
        selectedCompendiumSlotIndex.Value == compendiumDraft.SlotIndex;

    internal static PersonaSlotEdit PreserveCompendiumPersonaIdentity(
        PersonaSlotViewState currentSlot,
        PersonaSlotEdit personaSlotEdit)
    {
        ArgumentNullException.ThrowIfNull(currentSlot);
        ArgumentNullException.ThrowIfNull(personaSlotEdit);

        return personaSlotEdit with { PersonaId = currentSlot.PersonaId };
    }

    internal static bool TryBuildSocialLinkEdits(
        int? selectedSocialLinkIndex,
        string levelText,
        string progressText,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (!selectedSocialLinkIndex.HasValue)
        {
            return true;
        }

        bool levelIsValid = TryReadSocialLinkField(levelText, "Level", "SocialLinks.Level", diagnostics, out byte level);
        bool progressIsValid = TryReadSocialLinkField(progressText, "Progress", "SocialLinks.Progress", diagnostics, out byte progress);
        if (!levelIsValid || !progressIsValid)
        {
            return false;
        }

        edits.Add(new SetSocialLinkLevelEdit(selectedSocialLinkIndex.Value, level));
        edits.Add(new SetSocialLinkProgressEdit(selectedSocialLinkIndex.Value, progress));
        return true;
    }

    internal static bool TryAppendSelectedInventoryQuantityEdit(
        bool quantityDraftDirty,
        ushort? selectedInventoryItemId,
        string quantityText,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (!quantityDraftDirty || !selectedInventoryItemId.HasValue)
        {
            return true;
        }

        if (!TryReadInventoryQuantityText(quantityText, out byte quantity, out SaveDiagnostic diagnostic))
        {
            diagnostics.Add(diagnostic);
            return false;
        }

        edits.Add(new SetInventoryItemQuantityEdit(selectedInventoryItemId.Value, quantity));
        return true;
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

    internal static bool ShouldSkipSocialStatEdit(SocialStatViewState currentStat, SocialStatRankChoiceViewState selectedRank)
    {
        ArgumentNullException.ThrowIfNull(currentStat);
        ArgumentNullException.ThrowIfNull(selectedRank);

        return currentStat.Rank == selectedRank.Rank;
    }

    internal static bool ShouldSkipCalendarPhaseEdit(int currentPhaseId, CalendarPhaseChoiceViewState selectedPhase)
    {
        ArgumentNullException.ThrowIfNull(selectedPhase);

        return selectedPhase.PhaseId == currentPhaseId;
    }

    internal static Group4EditInputs CreateGroup4EditInputs(
        SocialStatRankChoiceViewState? courageRank,
        SocialStatRankChoiceViewState? knowledgeRank,
        SocialStatRankChoiceViewState? expressionRank,
        SocialStatRankChoiceViewState? understandingRank,
        SocialStatRankChoiceViewState? diligenceRank,
        string dayText,
        CalendarPhaseChoiceViewState? dayPhase,
        string nextDayText,
        CalendarPhaseChoiceViewState? nextPhase) =>
        new(
            courageRank,
            knowledgeRank,
            expressionRank,
            understandingRank,
            diligenceRank,
            dayText,
            dayPhase,
            nextDayText,
            nextPhase);

    private bool TryBuildPersonaSlotEdit(
        out PersonaSlotEdit personaSlotEdit,
        out SaveDiagnostic diagnostic,
        uint? maximumTotalExperience = null)
    {
        personaSlotEdit = new PersonaSlotEdit(0, 0, 0, Array.Empty<ushort>(), 0, 0, 0, 0, 0);
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");

        if (!TryReadSelectedPersonaId(out ushort personaId, out diagnostic))
        {
            return false;
        }

        if (!TryReadSelectedSkillIds(out IReadOnlyList<ushort> skillIds, out diagnostic))
        {
            return false;
        }

        return TryBuildPersonaSlotEditCore(
            personaId,
            PersonaXpTextBox.Text ?? string.Empty,
            skillIds,
            PersonaLevelSlider.Value,
            PersonaStrengthSlider.Value,
            PersonaMagicSlider.Value,
            PersonaEnduranceSlider.Value,
            PersonaAgilitySlider.Value,
            PersonaLuckSlider.Value,
            out personaSlotEdit,
            out diagnostic,
            maximumTotalExperience,
            allowNonBlankLevelZero: selectedCompendiumSlotIndex.HasValue);
    }

    private SaveEditorOperationResult SelectOrAddCompendiumPersona(PersonaChoiceViewState selectedChoice) =>
        SelectOrAddCompendiumPersonaCore(
            viewModel.CompendiumPersonaSlots,
            selectedChoice,
            viewModel.SetCompendiumPersonaSlot,
            slotIndex => selectedCompendiumSlotIndex = slotIndex);

    internal static SaveEditorOperationResult SelectOrAddCompendiumPersonaCore(
        IReadOnlyList<PersonaSlotViewState> compendiumPersonaSlots,
        PersonaChoiceViewState selectedChoice,
        Func<int, PersonaSlotEdit, SaveEditorOperationResult> setCompendiumPersonaSlot,
        Action<int> setSelectedCompendiumSlotIndex)
    {
        ArgumentNullException.ThrowIfNull(compendiumPersonaSlots);
        ArgumentNullException.ThrowIfNull(selectedChoice);
        ArgumentNullException.ThrowIfNull(setCompendiumPersonaSlot);
        ArgumentNullException.ThrowIfNull(setSelectedCompendiumSlotIndex);

        if (selectedChoice.PersonaId == 0)
        {
            return new SaveEditorOperationResult(true, []);
        }

        if (!TryResolveCompendiumPersonaAddTarget(
                compendiumPersonaSlots,
                selectedChoice.PersonaId,
                out int slotIndex,
                out bool existingSlot,
                out SaveDiagnostic? diagnostic))
        {
            return new SaveEditorOperationResult(false, [diagnostic!]);
        }

        if (existingSlot)
        {
            setSelectedCompendiumSlotIndex(slotIndex);
            return new SaveEditorOperationResult(true, []);
        }

        SaveEditorOperationResult result = setCompendiumPersonaSlot(slotIndex, CreateDefaultCompendiumPersonaSlotEdit(selectedChoice.PersonaId));
        if (result.Succeeded)
        {
            setSelectedCompendiumSlotIndex(slotIndex);
        }

        return result;
    }

    internal static bool TryResolveCompendiumPersonaAddTarget(
        IReadOnlyList<PersonaSlotViewState> compendiumPersonaSlots,
        ushort personaId,
        out int slotIndex,
        out bool existingSlot,
        out SaveDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(compendiumPersonaSlots);

        slotIndex = -1;
        existingSlot = false;
        diagnostic = null;

        for (int index = 0; index < compendiumPersonaSlots.Count; index++)
        {
            PersonaSlotViewState slot = compendiumPersonaSlots[index];
            if (slot.PersonaId == personaId)
            {
                slotIndex = slot.SlotIndex;
                existingSlot = true;
            }
        }

        if (existingSlot)
        {
            return true;
        }

        if (personaId > 0)
        {
            int legacySlotIndex = personaId - 1;
            if (legacySlotIndex < compendiumPersonaSlots.Count)
            {
                PersonaSlotViewState legacySlot = compendiumPersonaSlots[legacySlotIndex];
                if (legacySlot.PersonaId == 0)
                {
                    slotIndex = legacySlotIndex;
                    return true;
                }
            }
        }

        for (int index = 0; index < compendiumPersonaSlots.Count; index++)
        {
            PersonaSlotViewState slot = compendiumPersonaSlots[index];
            if (slot.PersonaId == 0)
            {
                slotIndex = slot.SlotIndex;
                return true;
            }
        }

        diagnostic = CreateUiDiagnostic("P4GWINUI027", "No free compendium slots are available.", "Compendium");
        return false;
    }

    internal static bool TryBuildPersonaSlotEditCore(
        ushort personaId,
        string totalExperienceText,
        IReadOnlyList<ushort> skillIds,
        double level,
        double strength,
        double magic,
        double endurance,
        double agility,
        double luck,
        out PersonaSlotEdit personaSlotEdit,
        out SaveDiagnostic diagnostic,
        uint? maximumTotalExperience = null,
        bool allowNonBlankLevelZero = true)
    {
        ArgumentNullException.ThrowIfNull(skillIds);

        personaSlotEdit = new PersonaSlotEdit(0, 0, 0, Array.Empty<ushort>(), 0, 0, 0, 0, 0);
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");

        if (!uint.TryParse(totalExperienceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint totalExperience))
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI015", "Persona total experience must be an unsigned whole number.", "Persona.Xp");
            return false;
        }

        if (maximumTotalExperience.HasValue && totalExperience > maximumTotalExperience.Value)
        {
            diagnostic = CreateUiDiagnostic(
                "P4GWINUI031",
                $"Compendium persona total experience must be at most {maximumTotalExperience.Value.ToString(CultureInfo.InvariantCulture)}.",
                "Persona.Xp");
            return false;
        }

        if (skillIds.Any(static skillId => skillId == ushort.MaxValue))
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI016", "Select a skill for each persona slot.", "Persona.Skills");
            return false;
        }

        byte roundedLevel = (byte)Math.Round(level, MidpointRounding.AwayFromZero);
        if (!allowNonBlankLevelZero && personaId != 0 && roundedLevel == 0)
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI032", "Non-blank persona level must be at least 1.", "Persona.Level");
            return false;
        }

        personaSlotEdit = new PersonaSlotEdit(
            personaId,
            roundedLevel,
            totalExperience,
            skillIds,
            (byte)Math.Round(strength, MidpointRounding.AwayFromZero),
            (byte)Math.Round(magic, MidpointRounding.AwayFromZero),
            (byte)Math.Round(endurance, MidpointRounding.AwayFromZero),
            (byte)Math.Round(agility, MidpointRounding.AwayFromZero),
            (byte)Math.Round(luck, MidpointRounding.AwayFromZero));
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");
        return true;
    }

    private static PersonaSlotEdit CreateDefaultCompendiumPersonaSlotEdit(ushort personaId) =>
        new(
            personaId,
            1,
            0,
            [0, 0, 0, 0, 0, 0, 1, 1],
            1,
            1,
            1,
            0,
            0);

    internal static double ResolvePersonaLevelAfterPersonaChoice(
        ushort selectedPersonaId,
        double currentLevel,
        bool isCompendiumContext) =>
        !isCompendiumContext &&
        selectedPersonaId != 0 &&
        (byte)Math.Round(currentLevel, MidpointRounding.AwayFromZero) == 0
            ? 1
            : currentLevel;

    internal static void MergeGroup4BatchResults(
        List<SaveEditCommand> batch,
        List<SaveDiagnostic> validationDiagnostics,
        IReadOnlyList<SaveEditCommand> group4Edits,
        IReadOnlyList<SaveDiagnostic> group4Diagnostics)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(validationDiagnostics);
        ArgumentNullException.ThrowIfNull(group4Edits);
        ArgumentNullException.ThrowIfNull(group4Diagnostics);

        batch.AddRange(group4Edits);
        validationDiagnostics.AddRange(group4Diagnostics);
    }

    internal static void AppendGroup4Edits(
        IReadOnlyList<SocialStatViewState> currentSocialStats,
        CalendarViewState currentCalendar,
        Group4EditInputs group4Inputs,
        List<SaveEditCommand> batch,
        List<SaveDiagnostic> validationDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(currentSocialStats);
        ArgumentNullException.ThrowIfNull(currentCalendar);
        ArgumentNullException.ThrowIfNull(group4Inputs);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(validationDiagnostics);

        Group4EditBatchBuilder.TryBuild(
            currentSocialStats,
            currentCalendar,
            group4Inputs,
            out IReadOnlyList<SaveEditCommand> group4Edits,
            out IReadOnlyList<SaveDiagnostic> group4Diagnostics);
        MergeGroup4BatchResults(batch, validationDiagnostics, group4Edits, group4Diagnostics);
    }

    internal static bool TryFinalizeEditBatch(
        List<SaveEditCommand> batch,
        List<SaveDiagnostic> validationDiagnostics,
        out IReadOnlyList<SaveEditCommand> edits,
        out IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(validationDiagnostics);

        edits = validationDiagnostics.Count == 0 ? batch : [];
        diagnostics = validationDiagnostics;
        return validationDiagnostics.Count == 0;
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

    private static ushort ReadPersonaId(ComboBox comboBox) =>
        comboBox.SelectedItem is PersonaChoiceViewState selectedPersona ? selectedPersona.PersonaId : ushort.MaxValue;

    internal static CompendiumPersonaViewState? ResolveSelectedCompendiumViewState(
        IReadOnlyList<CompendiumPersonaViewState> compendiumEntries,
        int? selectedCompendiumSlotIndex,
        bool autoSelectFirstVisibleEntry)
    {
        ArgumentNullException.ThrowIfNull(compendiumEntries);

        if (selectedCompendiumSlotIndex.HasValue)
        {
            CompendiumPersonaViewState? selectedEntry = compendiumEntries.FirstOrDefault(
                entry => entry.SlotIndex == selectedCompendiumSlotIndex.Value);
            if (selectedEntry is not null)
            {
                return selectedEntry;
            }
        }

        return autoSelectFirstVisibleEntry && compendiumEntries.Count > 0
            ? compendiumEntries[0]
            : null;
    }

    private void RefreshFromViewModel()
    {
        if (refreshEditableFieldsAfterStartupOpen)
        {
            RefreshBasicFieldsFromViewModel();
            return;
        }

        RefreshEditableFields();
    }

    private void RefreshFromViewModelPreservingInventoryQuantityDraft(
        bool preserveSelectedInventoryQuantityDraft = true,
        bool preserveSelectedSocialLinkDraft = true,
        bool preserveSelectedCompendiumDraft = true)
    {
        byte? selectedInventoryCategoryIdBeforeRefresh = selectedInventoryCategoryId;
        ushort? selectedInventoryItemIdBeforeRefresh = selectedInventoryItemId;
        ushort? selectedInventoryEntryIdBeforeRefresh = selectedInventoryEntryId;
        string inventoryQuantityDraft = InventoryQuantityTextBox.Text;
        bool inventoryQuantityDraftWasDirty = inventoryQuantityDraftDirty;
        SocialLinkDraftState? socialLinkDraft = CaptureSelectedSocialLinkDraft();
        CompendiumDraftState? compendiumDraft = preserveSelectedCompendiumDraft ? CaptureSelectedCompendiumDraft() : null;

        RefreshFromViewModel();

        if (preserveSelectedInventoryQuantityDraft &&
            inventoryQuantityDraftWasDirty &&
            InventorySelectionState.ShouldRestoreQuantityDraft(
                selectedInventoryCategoryIdBeforeRefresh,
                selectedInventoryItemIdBeforeRefresh,
                selectedInventoryEntryIdBeforeRefresh,
                selectedInventoryCategoryId,
                selectedInventoryItemId,
                selectedInventoryEntryId))
        {
            bool wasSuppressingInventoryEvents = suppressInventoryEvents;
            suppressInventoryEvents = true;
            try
            {
                InventoryQuantityTextBox.Text = inventoryQuantityDraft;
            }
            finally
            {
                suppressInventoryEvents = wasSuppressingInventoryEvents;
            }

            inventoryQuantityDraftDirty = true;
        }
        else
        {
            inventoryQuantityDraftDirty = false;
        }

        if (preserveSelectedSocialLinkDraft && socialLinkDraft is not null)
        {
            RestoreSelectedSocialLinkDraft(socialLinkDraft.Value);
        }

        if (compendiumDraft is not null)
        {
            RestoreSelectedCompendiumDraft(compendiumDraft.Value);
        }
    }

    internal static bool ShouldPreserveSelectedSocialLinkDraftAfterApply(IReadOnlyList<SaveEditCommand> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        return !edits.Any(static edit =>
            edit is SetSocialLinkLevelEdit or SetSocialLinkProgressEdit or SetSocialLinkFlagEdit);
    }

    internal static bool ShouldPreserveSelectedCompendiumDraftAfterApply(IReadOnlyList<SaveEditCommand> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        return !edits.Any(static edit =>
            edit is SetCompendiumPersonaSlotEdit or ClearCompendiumPersonaSlotEdit or ClearCompendiumPersonaSlotsEdit);
    }

    internal static bool ShouldPreserveSelectedInventoryQuantityDraftAfterApply(IReadOnlyList<SaveEditCommand> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        return !edits.Any(static edit => edit is SetInventoryItemQuantityEdit or RemoveInventoryItemEdit);
    }

    internal static bool ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(
        int? selectedCompendiumSlotIndexBeforeMutation,
        int? selectedCompendiumSlotIndexAfterMutation,
        bool mutationSucceeded,
        bool selectedCompendiumSlotMatchedRequestedPersonaBeforeMutation) =>
        !mutationSucceeded ||
        (selectedCompendiumSlotIndexBeforeMutation == selectedCompendiumSlotIndexAfterMutation &&
         selectedCompendiumSlotMatchedRequestedPersonaBeforeMutation);

    internal static int ResolveSelectedPersonaSlotIndexForProtagonistView(
        int selectedPersonaSlotIndex,
        IReadOnlyList<PersonaSlotViewState> personaSlots)
    {
        ArgumentNullException.ThrowIfNull(personaSlots);

        return personaSlots.Count == 0
            ? 0
            : Math.Clamp(selectedPersonaSlotIndex, 0, personaSlots.Count - 1);
    }

    internal static (byte? SelectedPersonaMemberId, int SelectedPersonaSlotIndex) PreserveSelectedPersonaSelectionDuringCompendiumRefresh(
        byte? selectedPersonaMemberId,
        int selectedPersonaSlotIndex) =>
        (selectedPersonaMemberId, selectedPersonaSlotIndex);

    internal static void ClearSelectedCompendiumContext(ref int? selectedCompendiumSlotIndex) =>
        selectedCompendiumSlotIndex = null;

    internal static SaveEditorOperationResult RefreshCompendiumDraftPreservingSelection(
        Func<SaveEditorOperationResult> mutateCompendium,
        Action<bool> refreshFromViewModelPreservingInventoryQuantityDraft,
        Action clearSelectedCompendiumSlotIndex)
    {
        ArgumentNullException.ThrowIfNull(mutateCompendium);
        ArgumentNullException.ThrowIfNull(refreshFromViewModelPreservingInventoryQuantityDraft);
        ArgumentNullException.ThrowIfNull(clearSelectedCompendiumSlotIndex);

        SaveEditorOperationResult result = mutateCompendium();
        if (result.Succeeded)
        {
            clearSelectedCompendiumSlotIndex();
        }

        refreshFromViewModelPreservingInventoryQuantityDraft(!result.Succeeded);
        return result;
    }

    private void RefreshEditableFields()
    {
        TraceStartup("RefreshEditableFields enter");
        RefreshBasicStatsState();
        RefreshSocialStatsState();
        RefreshCalendarState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        if (refreshEditableFieldsAfterStartupOpen)
        {
            if (!DispatcherQueue.TryEnqueue(RefreshRemainingEditableFields))
            {
                RefreshRemainingEditableFields();
            }
            return;
        }

        RefreshRemainingEditableFields();
        TraceStartup("RefreshEditableFields exit");
    }

    private void RefreshRemainingEditableFields()
    {
        TraceStartup("RefreshRemainingEditableFields enter");
        RefreshSocialLinksState();
        TraceStartup("RefreshRemainingEditableFields after social links");
        RefreshCompendiumState();
        TraceStartup("RefreshRemainingEditableFields after compendium");
        RefreshInventoryState();
        TraceStartup("RefreshRemainingEditableFields after inventory");
        RefreshPartyConfigurationState();
        TraceStartup("RefreshRemainingEditableFields after party");
        RefreshEquipmentState();
        TraceStartup("RefreshRemainingEditableFields after equipment");
        RefreshPersonaState();
        TraceStartup("RefreshRemainingEditableFields after persona");
        refreshEditableFieldsAfterStartupOpen = false;
        UpdateShellState();
        TraceStartup("RefreshRemainingEditableFields exit");
    }

    private void RefreshBasicFieldsFromViewModel()
    {
        RefreshBasicStatsState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
    }

    private void RefreshBasicStatsState()
    {
        suppressImmediateEditEvents = true;
        try
        {
            FamilyNameTextBox.Text = viewModel.FamilyName;
            GivenNameTextBox.Text = viewModel.GivenName;
            YenTextBox.Text = viewModel.HasSave ? viewModel.Yen.ToString(CultureInfo.InvariantCulture) : string.Empty;
            SetLevelSliderValue(MainCharacterLevelSlider, viewModel.HasSave ? viewModel.MainCharacterLevel : 0);
            UpdateMainCharacterLevelValueText();
            MainCharacterTotalExperienceTextBox.Text = viewModel.HasSave
                ? viewModel.MainCharacterTotalExperience.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
    }

    private void UpdateShellState()
    {
        bool startupRefreshPending = refreshEditableFieldsAfterStartupOpen;
        bool canEdit = viewModel.HasSave && !isBusy && !startupRefreshPending;
        bool canSave = canEdit && viewModel.CanWrite;
        bool canSaveAs = canSave;

        FileOpenMenuItem.IsEnabled = !isBusy && !startupRefreshPending;
        OpenButton.IsEnabled = !isBusy && !startupRefreshPending;
        ApplyButton.IsEnabled = canEdit;
        SaveButton.IsEnabled = canSave;
        SaveAsButton.IsEnabled = canSaveAs;
        FileSaveMenuItem.IsEnabled = canSave;
        FileSaveAsMenuItem.IsEnabled = canSaveAs;
        FamilyNameTextBox.IsEnabled = canEdit;
        GivenNameTextBox.IsEnabled = canEdit;
        YenTextBox.IsEnabled = canEdit;
        MainCharacterLevelSlider.IsEnabled = canEdit;
        MainCharacterTotalExperienceTextBox.IsEnabled = canEdit;
        MainCharacterCalculateFromLevelButton.IsEnabled = canEdit;
        CourageComboBox.IsEnabled = canEdit;
        KnowledgeComboBox.IsEnabled = canEdit;
        ExpressionComboBox.IsEnabled = canEdit;
        UnderstandingComboBox.IsEnabled = canEdit;
        DiligenceComboBox.IsEnabled = canEdit;
        DayTextBox.IsEnabled = canEdit;
        PhaseComboBox.IsEnabled = canEdit;
        NextDayTextBox.IsEnabled = canEdit;
        NextPhaseComboBox.IsEnabled = canEdit;
        SocialLinkListView.IsEnabled = canEdit;
        SocialLinkAddComboBox.IsEnabled = canEdit;
        SocialLinkLevelTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        SocialLinkProgressTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        SocialLinkDeleteButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        CompendiumListView.IsEnabled = canEdit;
        CompendiumAddComboBox.IsEnabled = canEdit;
        CompendiumRemoveButton.IsEnabled = canEdit && selectedCompendiumSlotIndex.HasValue;
        CompendiumClearButton.IsEnabled = canEdit && compendiumItems.Count > 0;
        PartySlot0ComboBox.IsEnabled = canEdit;
        PartySlot1ComboBox.IsEnabled = canEdit;
        PartySlot2ComboBox.IsEnabled = canEdit;
        PersonaMemberComboBox.IsEnabled = canEdit;
        PersonaSlotComboBox.IsEnabled = canEdit && selectedPersonaMemberId == 0 && !selectedCompendiumSlotIndex.HasValue;
        PersonaChoiceComboBox.IsEnabled = canEdit && !selectedCompendiumSlotIndex.HasValue;
        PersonaXpTextBox.IsEnabled = canEdit;
        PersonaLevelSlider.IsEnabled = canEdit;
        PersonaCalculateFromLevelButton.IsEnabled = canEdit;
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
        InventoryQuantityTextBox.IsEnabled = canEdit && selectedInventoryItemId.HasValue;
        InventoryAddUpdateButton.IsEnabled = canEdit && selectedInventoryItemId.HasValue;
        InventoryDeleteButton.IsEnabled = canEdit && selectedInventoryEntryId.HasValue && selectedInventoryItemId.HasValue;

        FilePathTextBlock.Text = ShellStateFormatter.GetFilePathText(currentFilePath);
        StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, viewModel.IsDirty || HasPendingEditorDrafts(), viewModel.CanWrite);
        UpdateWindowTitle();
    }

    private bool HasPendingEditorDrafts() =>
        viewModel.HasSave &&
        (HasBasicStatsDraft() || HasGroup4Draft() || HasSelectedSocialLinkDraft() || HasPersonaDraft() || inventoryQuantityDraftDirty);

    private bool HasBasicStatsDraft() =>
        !string.Equals(FamilyNameTextBox.Text ?? string.Empty, viewModel.FamilyName, StringComparison.Ordinal) ||
        !string.Equals(GivenNameTextBox.Text ?? string.Empty, viewModel.GivenName, StringComparison.Ordinal) ||
        !string.Equals(YenTextBox.Text ?? string.Empty, viewModel.Yen.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
        (byte)MainCharacterLevelSlider.Value != viewModel.MainCharacterLevel ||
        !string.Equals(
            MainCharacterTotalExperienceTextBox.Text ?? string.Empty,
            viewModel.MainCharacterTotalExperience.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private bool HasGroup4Draft()
    {
        if (!viewModel.HasSave || viewModel.SocialStats.Count < 5)
        {
            return false;
        }

        return HasSocialStatDraft(CourageComboBox, 0) ||
            HasSocialStatDraft(KnowledgeComboBox, 1) ||
            HasSocialStatDraft(ExpressionComboBox, 4) ||
            HasSocialStatDraft(UnderstandingComboBox, 3) ||
            HasSocialStatDraft(DiligenceComboBox, 2) ||
            !string.Equals(DayTextBox.Text ?? string.Empty, viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            HasCalendarPhaseDraft(PhaseComboBox, viewModel.Calendar.DayPhaseId) ||
            !string.Equals(NextDayTextBox.Text ?? string.Empty, viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            HasCalendarPhaseDraft(NextPhaseComboBox, viewModel.Calendar.NextDayPhaseId);
    }

    private bool HasSelectedSocialLinkDraft()
    {
        if (!selectedSocialLinkIndex.HasValue)
        {
            return false;
        }

        SocialLinkViewState? selectedLink = viewModel.SocialLinks.FirstOrDefault(link => link.SlotIndex == selectedSocialLinkIndex.Value);
        return selectedLink is not null &&
            (!string.Equals(SocialLinkLevelTextBox.Text ?? string.Empty, selectedLink.Level.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                !string.Equals(SocialLinkProgressTextBox.Text ?? string.Empty, selectedLink.Progress.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    private bool HasPersonaDraft()
    {
        PersonaSlotViewState? selectedSlot = GetSelectedPersonaSlotViewState();
        if (selectedSlot is null)
        {
            return false;
        }

        return ReadPersonaId(PersonaChoiceComboBox) != selectedSlot.PersonaId ||
            !string.Equals(PersonaXpTextBox.Text ?? string.Empty, selectedSlot.TotalExperience.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            (byte)PersonaLevelSlider.Value != selectedSlot.Level ||
            (byte)PersonaStrengthSlider.Value != selectedSlot.Strength ||
            (byte)PersonaMagicSlider.Value != selectedSlot.Magic ||
            (byte)PersonaEnduranceSlider.Value != selectedSlot.Endurance ||
            (byte)PersonaAgilitySlider.Value != selectedSlot.Agility ||
            (byte)PersonaLuckSlider.Value != selectedSlot.Luck ||
            ReadSkillId(PersonaSkillBox1) != selectedSlot.SkillIds[0] ||
            ReadSkillId(PersonaSkillBox2) != selectedSlot.SkillIds[1] ||
            ReadSkillId(PersonaSkillBox3) != selectedSlot.SkillIds[2] ||
            ReadSkillId(PersonaSkillBox4) != selectedSlot.SkillIds[3] ||
            ReadSkillId(PersonaSkillBox5) != selectedSlot.SkillIds[4] ||
            ReadSkillId(PersonaSkillBox6) != selectedSlot.SkillIds[5] ||
            ReadSkillId(PersonaSkillBox7) != selectedSlot.SkillIds[6] ||
            ReadSkillId(PersonaSkillBox8) != selectedSlot.SkillIds[7];
    }

    private PersonaSlotViewState? GetSelectedPersonaSlotViewState()
    {
        if (selectedCompendiumSlotIndex.HasValue &&
            (uint)selectedCompendiumSlotIndex.Value < (uint)viewModel.CompendiumPersonaSlots.Count)
        {
            return viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
        }

        if (!selectedPersonaMemberId.HasValue)
        {
            return null;
        }

        if (selectedPersonaMemberId.Value == 0)
        {
            return (uint)selectedPersonaSlotIndex < (uint)viewModel.ProtagonistPersonaSlots.Count
                ? viewModel.ProtagonistPersonaSlots[selectedPersonaSlotIndex]
                : null;
        }

        int partySlotIndex = selectedPersonaMemberId.Value - 1;
        return (uint)partySlotIndex < (uint)viewModel.PartyPersonaSlots.Count
            ? viewModel.PartyPersonaSlots[partySlotIndex]
            : null;
    }

    private bool HasSocialStatDraft(ComboBox comboBox, int statIndex) =>
        comboBox.SelectedItem is SocialStatRankChoiceViewState selectedRank &&
        !ShouldSkipSocialStatEdit(viewModel.SocialStats[statIndex], selectedRank);

    private static bool HasCalendarPhaseDraft(ComboBox comboBox, int currentPhaseId) =>
        comboBox.SelectedItem is CalendarPhaseChoiceViewState selectedPhase &&
        !ShouldSkipCalendarPhaseEdit(currentPhaseId, selectedPhase);

    private void RefreshSocialStatsState()
    {
        TraceStartup("RefreshSocialStatsState enter");
        if (!viewModel.HasSave || viewModel.SocialStats.Count == 0)
        {
            ClearSocialStatChoices(CourageComboBox);
            ClearSocialStatChoices(KnowledgeComboBox);
            ClearSocialStatChoices(ExpressionComboBox);
            ClearSocialStatChoices(UnderstandingComboBox);
            ClearSocialStatChoices(DiligenceComboBox);
            CourageComboBox.SelectedItem = null;
            KnowledgeComboBox.SelectedItem = null;
            ExpressionComboBox.SelectedItem = null;
            UnderstandingComboBox.SelectedItem = null;
            DiligenceComboBox.SelectedItem = null;
            return;
        }

        SetSocialStatSelection(CourageComboBox, 0);
        SetSocialStatSelection(KnowledgeComboBox, 1);
        SetSocialStatSelection(ExpressionComboBox, 4);
        SetSocialStatSelection(UnderstandingComboBox, 3);
        SetSocialStatSelection(DiligenceComboBox, 2);
        TraceStartup("RefreshSocialStatsState exit");
    }

    private void SetSocialStatSelection(ComboBox comboBox, int statIndex)
    {
        TraceStartup($"SetSocialStatSelection enter {statIndex}");
        suppressImmediateEditEvents = true;
        try
        {
            SocialStatViewState stat = viewModel.SocialStats[statIndex];
            comboBox.Items.Clear();
            IReadOnlyList<SocialStatRankChoiceViewState> choices = SaveEditorViewModel.GetSocialStatChoices(statIndex, stat.Points, out SocialStatRankChoiceViewState selectedChoice);
            foreach (SocialStatRankChoiceViewState choice in choices)
            {
                comboBox.Items.Add(choice);
            }

            comboBox.SelectedItem = selectedChoice;
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
        TraceStartup($"SetSocialStatSelection exit {statIndex}");
    }

    private static void ClearSocialStatChoices(ComboBox comboBox) =>
        comboBox.Items.Clear();

    private void RefreshCalendarState()
    {
        TraceStartup("RefreshCalendarState enter");
        suppressImmediateEditEvents = true;
        try
        {
            if (!viewModel.HasSave)
            {
                PhaseComboBox.Items.Clear();
                NextPhaseComboBox.Items.Clear();
                PhaseComboBox.SelectedItem = null;
                NextPhaseComboBox.SelectedItem = null;
                DayTextBox.Text = string.Empty;
                NextDayTextBox.Text = string.Empty;
                return;
            }

            DayTextBox.Text = viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture);
            NextDayTextBox.Text = viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture);
            PhaseComboBox.Items.Clear();
            IReadOnlyList<CalendarPhaseChoiceViewState> phaseChoices = SaveEditorViewModel.GetCalendarPhaseChoices(viewModel.Calendar.DayPhaseId, out CalendarPhaseChoiceViewState selectedPhase);
            foreach (CalendarPhaseChoiceViewState choice in phaseChoices)
            {
                PhaseComboBox.Items.Add(choice);
            }
            PhaseComboBox.SelectedItem = selectedPhase;
            NextPhaseComboBox.Items.Clear();
            IReadOnlyList<CalendarPhaseChoiceViewState> nextPhaseChoices = SaveEditorViewModel.GetCalendarPhaseChoices(viewModel.Calendar.NextDayPhaseId, out CalendarPhaseChoiceViewState selectedNextPhase);
            foreach (CalendarPhaseChoiceViewState choice in nextPhaseChoices)
            {
                NextPhaseComboBox.Items.Add(choice);
            }
            NextPhaseComboBox.SelectedItem = selectedNextPhase;
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
        TraceStartup("RefreshCalendarState exit");
    }

    private void RefreshSocialLinksState(bool allowFallbackSelection = false)
    {
        TraceStartup("RefreshSocialLinksState enter");
        suppressSocialLinkEvents = true;
        try
        {
            socialLinkItems.Clear();
            TraceStartup("RefreshSocialLinksState cleared list");
            if (viewModel.HasSave)
            {
                foreach (SocialLinkViewState socialLink in viewModel.SocialLinks)
                {
                    socialLinkItems.Add(socialLink);
                }
            }
            TraceStartup("RefreshSocialLinksState populated list");

            IReadOnlyList<SocialLinkChoiceViewState> linkChoices = SaveEditorViewModel.GetSocialLinkChoices(0, out SocialLinkChoiceViewState blankChoice);
            socialLinkChoices.Clear();
            TraceStartup("RefreshSocialLinksState cleared add choices");
            if (viewModel.HasSave)
            {
                foreach (SocialLinkChoiceViewState linkChoice in linkChoices)
                {
                    socialLinkChoices.Add(linkChoice);
                }
            }
            TraceStartup("RefreshSocialLinksState populated add choices");

            TraceStartup("RefreshSocialLinksState before selection-state check");
            if (!viewModel.HasSave || viewModel.SocialLinks.Count == 0)
            {
                ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);
                TraceStartup("RefreshSocialLinksState empty branch before list selection");
                SocialLinkListView.SelectedItem = null;
                TraceStartup("RefreshSocialLinksState empty branch after list selection");
                SocialLinkAddComboBox.SelectedItem = viewModel.HasSave ? blankChoice : null;
                TraceStartup("RefreshSocialLinksState empty branch after add selection");
                SocialLinkLevelTextBox.Text = string.Empty;
                SocialLinkProgressTextBox.Text = string.Empty;
                TraceStartup("RefreshSocialLinksState empty branch exit");
                return;
            }
            TraceStartup("RefreshSocialLinksState after selection-state check");

            TraceStartup("RefreshSocialLinksState resolving selection");
            SocialLinkViewState? selectedLink = ResolveSelectedSocialLinkViewState(
                viewModel.SocialLinks,
                selectedSocialLinkIndex,
                selectedSocialLinkLinkId,
                allowFallbackSelection);
            if (selectedLink is null)
            {
                ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);
                SocialLinkListView.SelectedItem = null;
                SocialLinkAddComboBox.SelectedItem = blankChoice;
                SocialLinkLevelTextBox.Text = string.Empty;
                SocialLinkProgressTextBox.Text = string.Empty;
                return;
            }
            TraceStartup("RefreshSocialLinksState resolved selection");
            selectedSocialLinkIndex = selectedLink.SlotIndex;
            selectedSocialLinkLinkId = selectedLink.LinkId;

            TraceStartup("RefreshSocialLinksState selecting list item");
            SocialLinkListView.SelectedItem = selectedLink;
            TraceStartup("RefreshSocialLinksState selected list item");
            SocialLinkAddComboBox.SelectedItem = blankChoice;
            TraceStartup("RefreshSocialLinksState selected add choice");
            SocialLinkLevelTextBox.Text = selectedLink.Level.ToString(CultureInfo.InvariantCulture);
            SocialLinkProgressTextBox.Text = selectedLink.Progress.ToString(CultureInfo.InvariantCulture);
            TraceStartup("RefreshSocialLinksState assigned selection");
        }
        finally
        {
            suppressSocialLinkEvents = false;
        }
        TraceStartup("RefreshSocialLinksState exit");
    }

    private void RefreshCompendiumState()
    {
        suppressCompendiumEvents = true;
        try
        {
            compendiumItems.Clear();
            PersonaChoiceViewState? blankChoice = null;
            if (viewModel.HasSave)
            {
                foreach (CompendiumPersonaViewState compendiumEntry in viewModel.CompendiumPersonaSlots
                    .Where(static slot => slot.PersonaId != 0)
                    .Select(slot =>
                    {
                        SaveEditorViewModel.GetPersonaChoices(slot.PersonaId, out PersonaChoiceViewState choice);
                        return new CompendiumPersonaViewState(
                            slot.SlotIndex,
                            slot.PersonaId,
                            choice.Name,
                            slot.Level,
                            slot.TotalExperience,
                            slot.Strength,
                            slot.Magic,
                            slot.Endurance,
                            slot.Agility,
                            slot.Luck);
                    })
                )
                {
                    compendiumItems.Add(compendiumEntry);
                }
                SaveEditorViewModel.GetPersonaChoices(0, out blankChoice);
            }

            compendiumAddChoices.Clear();
            if (viewModel.HasSave)
            {
                IReadOnlyList<PersonaChoiceViewState> addChoices = SaveEditorViewModel.GetPersonaChoices(0, out blankChoice);
                foreach (PersonaChoiceViewState addChoice in addChoices)
                {
                    compendiumAddChoices.Add(addChoice);
                }
            }

            CompendiumAddComboBox.SelectedItem = viewModel.HasSave ? blankChoice : null;

            if (!viewModel.HasSave || compendiumItems.Count == 0)
            {
                selectedCompendiumSlotIndex = null;
                CompendiumListView.SelectedItem = null;
                autoSelectCompendiumEntryAfterOpen = false;
                return;
            }

            CompendiumPersonaViewState? selectedEntry = ResolveSelectedCompendiumViewState(
                compendiumItems.ToArray(),
                selectedCompendiumSlotIndex,
                autoSelectCompendiumEntryAfterOpen);

            if (selectedEntry is not null)
            {
                selectedCompendiumSlotIndex = selectedEntry.SlotIndex;
                CompendiumListView.SelectedItem = selectedEntry;
            }
            else
            {
                selectedCompendiumSlotIndex = null;
                CompendiumListView.SelectedItem = null;
            }
            autoSelectCompendiumEntryAfterOpen = false;
            autoSelectCompendiumEntryAfterOpen = false;
        }
        finally
        {
            suppressCompendiumEvents = false;
        }
    }

    private void RefreshInventoryState()
    {
        suppressInventoryEvents = true;
        try
        {
            inventoryItems.Clear();
            foreach (InventoryStackViewState inventoryEntry in viewModel.HasSave
                ? viewModel.InventoryEntries
                : Array.Empty<InventoryStackViewState>())
            {
                inventoryItems.Add(inventoryEntry);
            }

            inventoryCategories.Clear();
            foreach (ItemCategoryViewState category in SaveEditorViewModel.InventoryCategories)
            {
                inventoryCategories.Add(category);
            }

            if (!viewModel.HasSave || SaveEditorViewModel.InventoryCategories.Count == 0)
            {
                autoSelectInventoryEntryAfterOpen = false;
                selectedInventoryCategoryId = null;
                selectedInventoryItemId = null;
                selectedInventoryEntryId = null;
                InventoryCategoryComboBox.SelectedItem = null;
                inventoryItemChoices.Clear();
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
                ? SaveEditorViewModel.InventoryCategories.FirstOrDefault(category => category.CategoryId == selectedInventoryCategoryId.Value)
                : null;
            if (selectedCategory is null && selectedEntry is not null)
            {
                selectedCategory = SaveEditorViewModel.InventoryCategories.FirstOrDefault(category => category.CategoryId == selectedEntry.CategoryId);
            }

            if (selectedCategory is null && selectedEntry is null && selectedInventoryCategoryId is null && selectedInventoryItemId is null)
            {
                InventoryCategoryComboBox.SelectedItem = null;
                inventoryItemChoices.Clear();
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
                ? SaveEditorViewModel.GetInventoryItemsForCategory(selectedCategory.CategoryId)
                : Array.Empty<InventoryItemChoiceViewState>();
            itemChoices = InventorySelectionProjection.ResolveItemChoices(
                itemChoices,
                selectedEntry,
                selectedInventoryItemId ?? (selectedCategory is not null
                    ? inventorySelectionState.GetRememberedCategoryItem(selectedCategory.CategoryId)
                    : null),
                out InventoryItemChoiceViewState? selectedItem);

            inventoryItemChoices.Clear();
            foreach (InventoryItemChoiceViewState itemChoice in itemChoices)
            {
                inventoryItemChoices.Add(itemChoice);
            }
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

    private void SocialLinkListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSocialLinkEvents)
        {
            return;
        }

        if (!TryApplySelectedSocialLinkDraftBeforeOperation())
        {
            RestoreSocialLinkListSelection();
            UpdateShellState();
            return;
        }

        if (SocialLinkListView.SelectedItem is not SocialLinkViewState selectedLink)
        {
            selectedSocialLinkIndex = null;
            selectedSocialLinkLinkId = null;
            RefreshSocialLinksState(allowFallbackSelection: selectedSocialLinkIndex.HasValue);
            UpdateShellState();
            return;
        }

        selectedSocialLinkIndex = selectedLink.SlotIndex;
        selectedSocialLinkLinkId = selectedLink.LinkId;
        RefreshSocialLinksState();
        UpdateShellState();
    }

    private void SocialLinkAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSocialLinkEvents)
        {
            return;
        }

        if (SocialLinkAddComboBox.SelectedItem is not SocialLinkChoiceViewState selectedChoice ||
            selectedChoice.IsPlaceholder)
        {
            return;
        }

        byte addLinkId = selectedChoice.LinkId;
        if (!TryApplySelectedSocialLinkDraftBeforeOperation())
        {
            ResetSocialLinkAddChoice();
            UpdateShellState();
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.AddSocialLink(addLinkId));
        RefreshSocialLinksState(allowFallbackSelection: selectedSocialLinkIndex.HasValue);
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Social link add failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private bool TryApplySelectedSocialLinkDraftBeforeOperation()
    {
        if (!viewModel.HasSave || !selectedSocialLinkIndex.HasValue)
        {
            return true;
        }

        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> validationDiagnostics = [];
        if (!TryAppendSelectedSocialLinkEdits(edits, validationDiagnostics))
        {
            SetUiDiagnostics(validationDiagnostics);
            return false;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.ApplyEdits(edits));
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            return false;
        }

        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        return true;
    }

    private void RestoreSocialLinkListSelection()
    {
        suppressSocialLinkEvents = true;
        try
        {
            SocialLinkListView.SelectedItem = GetSelectedSocialLinkViewState();
        }
        finally
        {
            suppressSocialLinkEvents = false;
        }
    }

    private void ResetSocialLinkAddChoice()
    {
        suppressSocialLinkEvents = true;
        try
        {
            SocialLinkAddComboBox.SelectedItem = socialLinkChoices.FirstOrDefault(static choice => choice.IsPlaceholder);
        }
        finally
        {
            suppressSocialLinkEvents = false;
        }
    }

    private void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI022", "Open a save before editing social links.", "SocialLinks")]);
            return;
        }

        if (!selectedSocialLinkIndex.HasValue)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI023", "Select a social link before deleting it.", "SocialLinks.Item")]);
            return;
        }

        if (!TryApplySelectedSocialLinkDraftBeforeOperation())
        {
            UpdateShellState();
            return;
        }

        uiDiagnosticsOverride = null;
        int deletedSlotIndex = selectedSocialLinkIndex.Value;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.RemoveSocialLink(deletedSlotIndex));
        if (result.Succeeded)
        {
            ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);
        }

        RefreshSocialLinksState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Social link delete failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressCompendiumEvents)
        {
            return;
        }

        if (!TryApplySelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (CompendiumListView.SelectedItem is not CompendiumPersonaViewState selectedEntry)
        {
            selectedCompendiumSlotIndex = null;
            RefreshPersonaState();
            UpdateShellState();
            return;
        }

        selectedCompendiumSlotIndex = selectedEntry.SlotIndex;
        RefreshPersonaState();
        UpdateShellState();
    }

    private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressCompendiumEvents)
        {
            return;
        }

        if (CompendiumAddComboBox.SelectedItem is not PersonaChoiceViewState selectedChoice)
        {
            return;
        }

        if (!TryApplySelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (selectedChoice.PersonaId == 0)
        {
            autoSelectCompendiumEntryAfterOpen = false;
            selectedCompendiumSlotIndex = null;
            suppressCompendiumEvents = true;
            try
            {
                CompendiumListView.SelectedItem = null;
            }
            finally
            {
                suppressCompendiumEvents = false;
            }

            RefreshPersonaState();
            UpdateShellState();
            return;
        }

        uiDiagnosticsOverride = null;
        int? selectedCompendiumSlotIndexBeforeMutation = selectedCompendiumSlotIndex;
        bool selectedCompendiumSlotMatchedRequestedPersonaBeforeMutation =
            selectedCompendiumSlotIndexBeforeMutation.HasValue &&
            selectedCompendiumSlotIndexBeforeMutation.Value < viewModel.CompendiumPersonaSlots.Count &&
            viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndexBeforeMutation.Value].PersonaId == selectedChoice.PersonaId;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => SelectOrAddCompendiumPersona(selectedChoice));
        RefreshFromViewModelPreservingInventoryQuantityDraft(
            preserveSelectedCompendiumDraft: ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(
                selectedCompendiumSlotIndexBeforeMutation,
                selectedCompendiumSlotIndex,
                result.Succeeded,
                selectedCompendiumSlotMatchedRequestedPersonaBeforeMutation));
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Compendium add failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI025", "Open a save before editing the compendium.", "Compendium")]);
            return;
        }

        if (!selectedCompendiumSlotIndex.HasValue)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI026", "Select a compendium entry before removing it.", "Compendium.Item")]);
            return;
        }

        uiDiagnosticsOverride = null;
        int removedSlotIndex = selectedCompendiumSlotIndex.Value;
        SaveEditorOperationResult result = RefreshCompendiumDraftPreservingSelection(
            () => saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.ClearCompendiumPersonaSlot(removedSlotIndex)),
            preserveSelectedCompendiumDraft => RefreshFromViewModelPreservingInventoryQuantityDraft(
                preserveSelectedCompendiumDraft: preserveSelectedCompendiumDraft),
            () => selectedCompendiumSlotIndex = null);
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Compendium remove failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void CompendiumClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI025", "Open a save before editing the compendium.", "Compendium")]);
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = RefreshCompendiumDraftPreservingSelection(
            () => saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.ClearCompendiumPersonaSlots()),
            preserveSelectedCompendiumDraft => RefreshFromViewModelPreservingInventoryQuantityDraft(
                preserveSelectedCompendiumDraft: preserveSelectedCompendiumDraft),
            () => selectedCompendiumSlotIndex = null);
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Compendium clear failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void RefreshPartyConfigurationState()
    {
        suppressImmediateEditEvents = true;
        try
        {
            SetPartyConfigurationChoices(partySlot0Choices, PartySlot0ComboBox, 0);
            SetPartyConfigurationChoices(partySlot1Choices, PartySlot1ComboBox, 1);
            SetPartyConfigurationChoices(partySlot2Choices, PartySlot2ComboBox, 2);
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
    }

    private void SetPartyConfigurationChoices(
        ObservableCollection<PartyConfigurationChoiceViewState> targetCollection,
        ComboBox comboBox,
        int slotIndex)
    {
        targetCollection.Clear();
        if (!viewModel.HasSave || viewModel.PartyMembers.Count <= slotIndex)
        {
            comboBox.SelectedItem = null;
            return;
        }

        byte currentMemberValue = viewModel.PartyMembers[slotIndex].MemberValue;
        IReadOnlyList<PartyConfigurationChoiceViewState> choices =
            SaveEditorViewModel.GetPartyConfigurationChoices(currentMemberValue, out PartyConfigurationChoiceViewState selectedChoice);
        foreach (PartyConfigurationChoiceViewState choice in choices)
        {
            targetCollection.Add(choice);
        }

        comboBox.SelectedItem = selectedChoice;
    }

    private void RefreshEquipmentState()
    {
        suppressEquipmentEvents = true;
        try
        {
            equipmentCharacters.Clear();
            foreach (EquipmentCharacterViewState character in viewModel.HasSave
                ? viewModel.EquipmentCharacters
                : Array.Empty<EquipmentCharacterViewState>())
            {
                equipmentCharacters.Add(character);
            }

            if (!viewModel.HasSave || equipmentCharacters.Count == 0)
            {
                selectedEquipmentCharacterId = null;
                EquipmentCharacterComboBox.SelectedItem = null;
                equipmentWeaponChoices.Clear();
                equipmentArmorChoices.Clear();
                equipmentAccessoryChoices.Clear();
                equipmentCostumeChoices.Clear();
                EquipmentWeaponComboBox.SelectedItem = null;
                EquipmentArmorComboBox.SelectedItem = null;
                EquipmentAccessoryComboBox.SelectedItem = null;
                EquipmentCostumeComboBox.SelectedItem = null;
                return;
            }

            EquipmentCharacterViewState? selectedCharacter = null;
            if (selectedEquipmentCharacterId.HasValue)
            {
                selectedCharacter = equipmentCharacters.FirstOrDefault(
                    character => character.CharacterId == selectedEquipmentCharacterId.Value);
            }

            selectedCharacter ??= equipmentCharacters[0];
            EquipmentCharacterComboBox.SelectedItem = selectedCharacter;
            selectedEquipmentCharacterId = selectedCharacter.CharacterId;

            SetEquipmentChoices(equipmentWeaponChoices, EquipmentWeaponComboBox, SaveEditorViewModel.GetWeaponChoices(selectedCharacter.CharacterId), selectedCharacter.WeaponItemId);
            SetEquipmentChoices(equipmentArmorChoices, EquipmentArmorComboBox, SaveEditorViewModel.GetArmorChoices(), selectedCharacter.ArmorItemId);
            SetEquipmentChoices(equipmentAccessoryChoices, EquipmentAccessoryComboBox, SaveEditorViewModel.GetAccessoryChoices(), selectedCharacter.AccessoryItemId);
            SetEquipmentChoices(equipmentCostumeChoices, EquipmentCostumeComboBox, SaveEditorViewModel.GetCostumeChoices(), selectedCharacter.CostumeItemId);
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
            personaMemberChoices.Clear();
            foreach (PartyMemberChoiceViewState memberChoice in viewModel.HasSave
                ? viewModel.PartyMemberChoices
                : Array.Empty<PartyMemberChoiceViewState>())
            {
                personaMemberChoices.Add(memberChoice);
            }

            if (!viewModel.HasSave || personaMemberChoices.Count == 0)
            {
                selectedPersonaMemberId = null;
                selectedPersonaSlotIndex = 0;
                PersonaMemberComboBox.SelectedItem = null;
                personaSlotChoices.Clear();
                PersonaSlotComboBox.SelectedItem = null;
                personaChoices.Clear();
                PersonaChoiceComboBox.SelectedItem = null;
                PersonaXpTextBox.Text = string.Empty;
                SetLevelSliderValue(PersonaLevelSlider, 0);
                PersonaStrengthSlider.Value = 0;
                PersonaMagicSlider.Value = 0;
                PersonaEnduranceSlider.Value = 0;
                PersonaAgilitySlider.Value = 0;
                PersonaLuckSlider.Value = 0;
                PersonaSkillBox1.SelectedItem = null;
                PersonaSkillBox2.SelectedItem = null;
                PersonaSkillBox3.SelectedItem = null;
                PersonaSkillBox4.SelectedItem = null;
                PersonaSkillBox5.SelectedItem = null;
                PersonaSkillBox6.SelectedItem = null;
                PersonaSkillBox7.SelectedItem = null;
                PersonaSkillBox8.SelectedItem = null;
                return;
            }

            if (selectedCompendiumSlotIndex.HasValue)
            {
                PersonaSlotViewState currentCompendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
                (selectedPersonaMemberId, selectedPersonaSlotIndex) =
                    PreserveSelectedPersonaSelectionDuringCompendiumRefresh(selectedPersonaMemberId, selectedPersonaSlotIndex);
                PersonaMemberComboBox.SelectedItem = null;
                personaSlotChoices.Clear();
                PersonaSlotComboBox.SelectedItem = null;
                personaChoices.Clear();
                PersonaChoiceViewState selectedCompendiumChoice = default!;
                foreach (PersonaChoiceViewState choice in SaveEditorViewModel.GetPersonaChoices(
                    currentCompendiumSlot.PersonaId,
                    out selectedCompendiumChoice))
                {
                    personaChoices.Add(choice);
                }
                PersonaChoiceComboBox.SelectedItem = selectedCompendiumChoice;
                PersonaXpTextBox.Text = currentCompendiumSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
                SetLevelSliderValue(PersonaLevelSlider, currentCompendiumSlot.Level);
                PersonaStrengthSlider.Value = currentCompendiumSlot.Strength;
                PersonaMagicSlider.Value = currentCompendiumSlot.Magic;
                PersonaEnduranceSlider.Value = currentCompendiumSlot.Endurance;
                PersonaAgilitySlider.Value = currentCompendiumSlot.Agility;
                PersonaLuckSlider.Value = currentCompendiumSlot.Luck;
                SetPersonaSkillChoices(currentCompendiumSlot.SkillIds);
                UpdateShellState();
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
                personaSlotChoices.Clear();
                PersonaSlotComboBox.SelectedItem = null;
                return;
            }

            if (isProtagonist)
            {
                selectedPersonaSlotIndex = ResolveSelectedPersonaSlotIndexForProtagonistView(
                    selectedPersonaSlotIndex,
                    personaSlots);
                personaSlotChoices.Clear();
                foreach (PersonaSlotViewState slot in personaSlots)
                {
                    personaSlotChoices.Add(slot);
                }
                PersonaSlotComboBox.SelectedItem = personaSlots[selectedPersonaSlotIndex];
            }
            else
            {
                int partyPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1, 0, personaSlots.Count - 1);
                personaSlotChoices.Clear();
                PersonaSlotComboBox.SelectedItem = null;
                personaChoices.Clear();
                PersonaChoiceComboBox.SelectedItem = null;
                PersonaXpTextBox.Text = string.Empty;
                SetLevelSliderValue(PersonaLevelSlider, 0);
                PersonaStrengthSlider.Value = 0;
                PersonaMagicSlider.Value = 0;
                PersonaEnduranceSlider.Value = 0;
                PersonaAgilitySlider.Value = 0;
                PersonaLuckSlider.Value = 0;
                personaSkillChoices1.Clear();
                PersonaSkillBox1.SelectedItem = null;
                personaSkillChoices2.Clear();
                PersonaSkillBox2.SelectedItem = null;
                personaSkillChoices3.Clear();
                PersonaSkillBox3.SelectedItem = null;
                personaSkillChoices4.Clear();
                PersonaSkillBox4.SelectedItem = null;
                personaSkillChoices5.Clear();
                PersonaSkillBox5.SelectedItem = null;
                personaSkillChoices6.Clear();
                PersonaSkillBox6.SelectedItem = null;
                personaSkillChoices7.Clear();
                PersonaSkillBox7.SelectedItem = null;
                personaSkillChoices8.Clear();
                PersonaSkillBox8.SelectedItem = null;
                PersonaSlotViewState partyCurrentSlot = personaSlots[partyPersonaSlotIndex];
                personaChoices.Clear();
                PersonaChoiceViewState partySelectedPersonaChoice = default!;
                foreach (PersonaChoiceViewState choice in SaveEditorViewModel.GetPersonaChoices(
                    partyCurrentSlot.PersonaId,
                    out partySelectedPersonaChoice))
                {
                    personaChoices.Add(choice);
                }
                PersonaChoiceComboBox.SelectedItem = partySelectedPersonaChoice;
                PersonaXpTextBox.Text = partyCurrentSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
                SetLevelSliderValue(PersonaLevelSlider, partyCurrentSlot.Level);
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
            personaChoices.Clear();
            PersonaChoiceViewState selectedPersonaChoice = default!;
            foreach (PersonaChoiceViewState choice in SaveEditorViewModel.GetPersonaChoices(currentSlot.PersonaId, out selectedPersonaChoice))
            {
                personaChoices.Add(choice);
            }
            PersonaChoiceComboBox.SelectedItem = selectedPersonaChoice;
            PersonaXpTextBox.Text = currentSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
            SetLevelSliderValue(PersonaLevelSlider, currentSlot.Level);
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
            UpdatePersonaLevelValueText();
        }
    }

    private static void SetEquipmentChoices(
        ObservableCollection<InventoryItemChoiceViewState> targetCollection,
        ComboBox comboBox,
        IReadOnlyList<InventoryItemChoiceViewState> itemChoices,
        ushort selectedItemId)
    {
        targetCollection.Clear();
        IReadOnlyList<InventoryItemChoiceViewState> choices = InventorySelectionProjection.ResolveEquipmentChoices(
            itemChoices,
            selectedItemId,
            out InventoryItemChoiceViewState? selectedItem);
        foreach (InventoryItemChoiceViewState choice in choices)
        {
            targetCollection.Add(choice);
        }
        comboBox.SelectedItem = selectedItem;
    }

    private static void SetSkillChoices(
        ObservableCollection<SkillChoiceViewState> targetCollection,
        IReadOnlyList<SkillChoiceViewState> skillChoices)
    {
        targetCollection.Clear();
        foreach (SkillChoiceViewState skillChoice in skillChoices)
        {
            targetCollection.Add(skillChoice);
        }
    }

    private void SetPersonaSkillChoices(IReadOnlyList<ushort> skillIds)
    {
        IReadOnlyList<SkillChoiceViewState> skill1Choices = SaveEditorViewModel.GetSkillChoices(skillIds[0], out SkillChoiceViewState skill1);
        IReadOnlyList<SkillChoiceViewState> skill2Choices = SaveEditorViewModel.GetSkillChoices(skillIds[1], out SkillChoiceViewState skill2);
        IReadOnlyList<SkillChoiceViewState> skill3Choices = SaveEditorViewModel.GetSkillChoices(skillIds[2], out SkillChoiceViewState skill3);
        IReadOnlyList<SkillChoiceViewState> skill4Choices = SaveEditorViewModel.GetSkillChoices(skillIds[3], out SkillChoiceViewState skill4);
        IReadOnlyList<SkillChoiceViewState> skill5Choices = SaveEditorViewModel.GetSkillChoices(skillIds[4], out SkillChoiceViewState skill5);
        IReadOnlyList<SkillChoiceViewState> skill6Choices = SaveEditorViewModel.GetSkillChoices(skillIds[5], out SkillChoiceViewState skill6);
        IReadOnlyList<SkillChoiceViewState> skill7Choices = SaveEditorViewModel.GetSkillChoices(skillIds[6], out SkillChoiceViewState skill7);
        IReadOnlyList<SkillChoiceViewState> skill8Choices = SaveEditorViewModel.GetSkillChoices(skillIds[7], out SkillChoiceViewState skill8);

        SetSkillChoices(personaSkillChoices1, skill1Choices);
        PersonaSkillBox1.SelectedItem = skill1;
        SetSkillChoices(personaSkillChoices2, skill2Choices);
        PersonaSkillBox2.SelectedItem = skill2;
        SetSkillChoices(personaSkillChoices3, skill3Choices);
        PersonaSkillBox3.SelectedItem = skill3;
        SetSkillChoices(personaSkillChoices4, skill4Choices);
        PersonaSkillBox4.SelectedItem = skill4;
        SetSkillChoices(personaSkillChoices5, skill5Choices);
        PersonaSkillBox5.SelectedItem = skill5;
        SetSkillChoices(personaSkillChoices6, skill6Choices);
        PersonaSkillBox6.SelectedItem = skill6;
        SetSkillChoices(personaSkillChoices7, skill7Choices);
        PersonaSkillBox7.SelectedItem = skill7;
        SetSkillChoices(personaSkillChoices8, skill8Choices);
        PersonaSkillBox8.SelectedItem = skill8;
    }

    private byte GetInventoryQuantityOrDefault(ushort itemId)
    {
        InventoryStackViewState? entry = viewModel.InventoryEntries.FirstOrDefault(stack => stack.ItemId == itemId);
        return entry?.Quantity ?? (byte)1;
    }

    private bool TryApplySelectedInventoryQuantityDraftBeforeOperation()
    {
        if (!viewModel.HasSave || !inventoryQuantityDraftDirty || !selectedInventoryItemId.HasValue)
        {
            return true;
        }

        if (!TryReadInventoryQuantity(out byte quantity))
        {
            return false;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.SetInventoryItemQuantity(selectedInventoryItemId.Value, quantity));
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            return false;
        }

        inventoryQuantityDraftDirty = false;
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        return true;
    }

    private void RestoreInventorySelectionAfterBlockedDraft() =>
        RefreshFromViewModelPreservingInventoryQuantityDraft();

    private bool TryAutoAddSelectedInventoryItem()
    {
        if (!selectedInventoryItemId.HasValue)
        {
            return true;
        }

        InventoryStackViewState? existingEntry = viewModel.InventoryEntries.FirstOrDefault(entry => entry.ItemId == selectedInventoryItemId.Value);
        if (existingEntry is not null)
        {
            selectedInventoryEntryId = existingEntry.ItemId;
            return true;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.SetInventoryItemQuantity(selectedInventoryItemId.Value, 1));
        if (result.Succeeded)
        {
            selectedInventoryEntryId = selectedInventoryItemId.Value;
            return true;
        }

        SetUiDiagnostics(result.Diagnostics);
        return false;
    }

    private bool TryApplySelectedPersonaDraftBeforeOperation()
    {
        if (!viewModel.HasSave || (!selectedCompendiumSlotIndex.HasValue && !selectedPersonaMemberId.HasValue))
        {
            return true;
        }

        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> validationDiagnostics = [];
        AddPersonaEdit(edits, validationDiagnostics);
        if (validationDiagnostics.Count > 0)
        {
            SetUiDiagnostics(validationDiagnostics);
            return false;
        }

        if (edits.Count == 0)
        {
            return true;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.ApplyEdits(edits));
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            return false;
        }

        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        return true;
    }

    private void RestorePersonaSelectionAfterBlockedDraft()
    {
        bool wasSuppressingPersonaEvents = suppressPersonaEvents;
        bool wasSuppressingCompendiumEvents = suppressCompendiumEvents;
        suppressPersonaEvents = true;
        suppressCompendiumEvents = true;
        try
        {
            if (selectedCompendiumSlotIndex.HasValue)
            {
                CompendiumListView.SelectedItem = compendiumItems.FirstOrDefault(
                    entry => entry.SlotIndex == selectedCompendiumSlotIndex.Value);
                PersonaMemberComboBox.SelectedItem = null;
                PersonaSlotComboBox.SelectedItem = null;
            }
            else
            {
                PersonaMemberComboBox.SelectedItem = selectedPersonaMemberId.HasValue
                    ? personaMemberChoices.FirstOrDefault(member => member.MemberId == selectedPersonaMemberId.Value)
                    : null;
                PersonaSlotComboBox.SelectedItem = selectedPersonaMemberId == 0
                    ? personaSlotChoices.FirstOrDefault(slot => slot.SlotIndex == selectedPersonaSlotIndex)
                    : null;
                CompendiumListView.SelectedItem = null;
            }

            CompendiumAddComboBox.SelectedItem = compendiumAddChoices.FirstOrDefault(static choice => choice.PersonaId == 0);
        }
        finally
        {
            suppressPersonaEvents = wasSuppressingPersonaEvents;
            suppressCompendiumEvents = wasSuppressingCompendiumEvents;
        }
    }

    private void InventoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressInventoryEvents)
        {
            return;
        }

        if (!TryApplySelectedInventoryQuantityDraftBeforeOperation())
        {
            RestoreInventorySelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (InventoryListView.SelectedItem is not InventoryStackViewState selectedEntry)
        {
            inventoryQuantityDraftDirty = false;
            selectedInventoryEntryId = null;
            UpdateShellState();
            return;
        }

        inventoryQuantityDraftDirty = false;
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

        if (!TryApplySelectedInventoryQuantityDraftBeforeOperation())
        {
            RestoreInventorySelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (InventoryCategoryComboBox.SelectedItem is ItemCategoryViewState selectedCategory)
        {
            inventoryQuantityDraftDirty = false;
            selectedInventoryCategoryId = selectedCategory.CategoryId;
            selectedInventoryItemId = null;
            selectedInventoryEntryId = null;
            RefreshInventoryState();
            _ = TryAutoAddSelectedInventoryItem();
            RefreshInventoryState();
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
        }
    }

    private void InventoryItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressInventoryEvents)
        {
            return;
        }

        if (!TryApplySelectedInventoryQuantityDraftBeforeOperation())
        {
            RestoreInventorySelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (InventoryItemComboBox.SelectedItem is InventoryItemChoiceViewState selectedItem)
        {
            inventoryQuantityDraftDirty = false;
            inventorySelectionState.RememberCategoryItem(selectedItem.CategoryId, selectedItem.ItemId);
            selectedInventoryCategoryId = selectedItem.CategoryId;
            selectedInventoryItemId = selectedItem.IsPlaceholder ? null : selectedItem.ItemId;
            InventoryStackViewState? selectedEntry = selectedItem.IsPlaceholder
                ? null
                : viewModel.InventoryEntries.FirstOrDefault(entry => entry.ItemId == selectedItem.ItemId);
            selectedInventoryEntryId = selectedEntry?.ItemId;
            _ = TryAutoAddSelectedInventoryItem();

            RefreshInventoryState();
            DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
            UpdateShellState();
            return;
        }

        inventoryQuantityDraftDirty = false;
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
            inventoryQuantityDraftDirty = false;
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
            inventoryQuantityDraftDirty = false;
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

        if (!TryApplySelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (PersonaMemberComboBox.SelectedItem is not PartyMemberChoiceViewState selectedMember)
        {
            selectedPersonaMemberId = null;
            RefreshPersonaSummary();
            UpdateShellState();
            return;
        }

        if (selectedCompendiumSlotIndex.HasValue)
        {
            ClearSelectedCompendiumContext(ref selectedCompendiumSlotIndex);
            suppressCompendiumEvents = true;
            try
            {
                CompendiumListView.SelectedItem = null;
            }
            finally
            {
                suppressCompendiumEvents = false;
            }
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

        if (!TryApplySelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
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

        if (PersonaChoiceComboBox.SelectedItem is PersonaChoiceViewState selectedPersonaChoice)
        {
            double resolvedLevel = ResolvePersonaLevelAfterPersonaChoice(
                selectedPersonaChoice.PersonaId,
                PersonaLevelSlider.Value,
                selectedCompendiumSlotIndex.HasValue);
            if (resolvedLevel != PersonaLevelSlider.Value)
            {
                SetLevelSliderValue(PersonaLevelSlider, resolvedLevel);
            }
        }

        RefreshPersonaDraftShellState();
    }

    private void PersonaSkillBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressPersonaEvents)
        {
            return;
        }

        RefreshPersonaDraftShellState();
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
        if (TryReadInventoryQuantityText(InventoryQuantityTextBox.Text ?? string.Empty, out quantity, out SaveDiagnostic diagnostic))
        {
            return true;
        }

        quantity = 0;
        SetUiDiagnostics([diagnostic]);
        return false;
    }

    private void InventoryQuantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressInventoryEvents || viewModel is null || !viewModel.HasSave || !selectedInventoryItemId.HasValue)
        {
            return;
        }

        inventoryQuantityDraftDirty = true;
        if (!TryReadInventoryQuantity(out byte quantity))
        {
            UpdateShellState();
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.SetInventoryItemQuantity(selectedInventoryItemId.Value, quantity));
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            UpdateShellState();
            return;
        }

        inventoryQuantityDraftDirty = false;
        if (quantity == 0)
        {
            inventorySelectionState.DisableAutoSelectAfterDelete();
            selectedInventoryCategoryId = null;
            selectedInventoryItemId = null;
            selectedInventoryEntryId = null;
        }
        else
        {
            selectedInventoryEntryId = selectedInventoryItemId.Value;
        }

        RefreshInventoryState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
    }

    private void FamilyNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyImmediateEdit(
            () => new SetSaveNamesEdit(FamilyNameTextBox.Text ?? string.Empty, GivenNameTextBox.Text ?? string.Empty),
            refreshAfterSuccess: false);

    private void GivenNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyImmediateEdit(
            () => new SetSaveNamesEdit(FamilyNameTextBox.Text ?? string.Empty, GivenNameTextBox.Text ?? string.Empty),
            refreshAfterSuccess: false);

    private void YenTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        if (!uint.TryParse(YenTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint yen))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI006", "Yen must be an unsigned whole number.", "Yen")]);
            UpdateShellState();
            return;
        }

        ApplyImmediateEdit(() => new SetYenEdit(yen), refreshAfterSuccess: false);
    }

    private void MainCharacterTotalExperienceTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        if (!uint.TryParse(MainCharacterTotalExperienceTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint totalExperience))
        {
            SetUiDiagnostics([CreateUiDiagnostic(
                "P4GWINUI028",
                "Main character total experience must be an unsigned whole number.",
                "MainCharacter.TotalExperience")]);
            UpdateShellState();
            return;
        }

        ApplyImmediateEdit(() => new SetMainCharacterTotalExperienceEdit(totalExperience), refreshAfterSuccess: false);
    }

    private void SocialStatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        if (ReferenceEquals(sender, CourageComboBox))
        {
            ApplyImmediateSocialStatEdit(CourageComboBox, 0);
        }
        else if (ReferenceEquals(sender, KnowledgeComboBox))
        {
            ApplyImmediateSocialStatEdit(KnowledgeComboBox, 1);
        }
        else if (ReferenceEquals(sender, ExpressionComboBox))
        {
            ApplyImmediateSocialStatEdit(ExpressionComboBox, 4);
        }
        else if (ReferenceEquals(sender, UnderstandingComboBox))
        {
            ApplyImmediateSocialStatEdit(UnderstandingComboBox, 3);
        }
        else if (ReferenceEquals(sender, DiligenceComboBox))
        {
            ApplyImmediateSocialStatEdit(DiligenceComboBox, 2);
        }
    }

    private void ApplyImmediateSocialStatEdit(ComboBox comboBox, int statIndex)
    {
        if (comboBox.SelectedItem is not SocialStatRankChoiceViewState selectedRank)
        {
            return;
        }

        if (ShouldSkipSocialStatEdit(viewModel.SocialStats[statIndex], selectedRank))
        {
            UpdateShellState();
            return;
        }

        ApplyImmediateEdit(() => new SetSocialStatRankEdit(statIndex, selectedRank.Rank), refreshAfterSuccess: true);
    }

    private void DayTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyImmediateDayEdit(DayTextBox.Text ?? string.Empty, false);

    private void NextDayTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyImmediateDayEdit(NextDayTextBox.Text ?? string.Empty, true);

    private void ApplyImmediateDayEdit(string text, bool isNextDay)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
        {
            SetUiDiagnostics([CreateUiDiagnostic(
                isNextDay ? "P4GWINUI020" : "P4GWINUI018",
                isNextDay ? "Next day must be a whole number." : "Day must be a whole number.",
                isNextDay ? "Calendar.NextDay" : "Calendar.Day")]);
            UpdateShellState();
            return;
        }

        ApplyImmediateEdit(
            () => isNextDay ? new SetNextDayEdit(day) : new SetDayEdit(day),
            refreshAfterSuccess: false);
    }

    private void PhaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        ApplyImmediatePhaseEdit(PhaseComboBox, viewModel.Calendar.DayPhaseId, false);
    }

    private void NextPhaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        ApplyImmediatePhaseEdit(NextPhaseComboBox, viewModel.Calendar.NextDayPhaseId, true);
    }

    private void ApplyImmediatePhaseEdit(ComboBox comboBox, int currentPhaseId, bool isNextPhase)
    {
        if (comboBox.SelectedItem is not CalendarPhaseChoiceViewState selectedPhase)
        {
            return;
        }

        if (ShouldSkipCalendarPhaseEdit(currentPhaseId, selectedPhase))
        {
            UpdateShellState();
            return;
        }

        ApplyImmediateEdit(
            () => isNextPhase ? new SetNextDayPhaseEdit(selectedPhase.PhaseId) : new SetDayPhaseEdit(selectedPhase.PhaseId),
            refreshAfterSuccess: false);
    }

    private void PartySlot0ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyImmediatePartyMemberEdit(PartySlot0ComboBox, 0);

    private void PartySlot1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyImmediatePartyMemberEdit(PartySlot1ComboBox, 1);

    private void PartySlot2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyImmediatePartyMemberEdit(PartySlot2ComboBox, 2);

    private void ApplyImmediatePartyMemberEdit(ComboBox comboBox, int slotIndex)
    {
        if (!CanProcessImmediateEditEvent() ||
            comboBox.SelectedItem is not PartyConfigurationChoiceViewState selectedMember)
        {
            return;
        }

        ApplyImmediateEdit(() => new SetPartyMemberEdit(slotIndex, selectedMember.MemberValue), refreshAfterSuccess: false);
    }

    private void SocialLinkTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressSocialLinkEvents || !CanProcessImmediateEditEvent() || !selectedSocialLinkIndex.HasValue)
        {
            return;
        }

        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];
        if (!TryAppendSelectedSocialLinkEdits(edits, diagnostics))
        {
            SetUiDiagnostics(diagnostics);
            UpdateShellState();
            return;
        }

        ApplyImmediateEdits(
            edits,
            refreshAfterSuccess: false,
            refreshAfterSuccessAction: RefreshSelectedSocialLinkRowSummary);
    }

    private bool CanProcessImmediateEditEvent() =>
        !suppressImmediateEditEvents &&
        viewModel is not null &&
        viewModel.HasSave &&
        !isBusy &&
        !refreshEditableFieldsAfterStartupOpen;

    private void ApplyImmediateEdit(Func<SaveEditCommand> createEdit, bool refreshAfterSuccess)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        ApplyImmediateEdits([createEdit()], refreshAfterSuccess);
    }

    private void ApplyImmediateEdits(
        List<SaveEditCommand> edits,
        bool refreshAfterSuccess,
        Action? refreshAfterSuccessAction = null)
    {
        if (!CanProcessImmediateEditEvent() || edits.Count == 0)
        {
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.ApplyEdits(edits));
        if (result.Succeeded)
        {
            if (refreshAfterSuccess)
            {
                RefreshBasicStatsState();
                RefreshSocialStatsState();
                RefreshCalendarState();
            }

            refreshAfterSuccessAction?.Invoke();
        }

        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
        }
    }

    internal static bool TryReadInventoryQuantityText(
        string quantityText,
        out byte quantity,
        out SaveDiagnostic diagnostic)
    {
        if (byte.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI011", "Inventory quantity must be a whole number from 0 to 255.", "Inventory.Quantity");
            return true;
        }

        quantity = 0;
        diagnostic = CreateUiDiagnostic("P4GWINUI011", "Inventory quantity must be a whole number from 0 to 255.", "Inventory.Quantity");
        return false;
    }

    internal static bool TryReadSocialLinkField(
        string? text,
        string fieldName,
        string target,
        List<SaveDiagnostic> diagnostics,
        out byte value)
    {
        if (byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        diagnostics.Add(CreateUiDiagnostic("P4GWINUI024", $"{fieldName} must be a whole number from 0 to 255.", target));
        return false;
    }

    private string BuildPersonaSummary()
    {
        if (!viewModel.HasSave)
        {
            return "Open a save to inspect persona slot projections.";
        }

        if (selectedCompendiumSlotIndex.HasValue)
        {
            PersonaSlotViewState compendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
            SaveEditorViewModel.GetPersonaChoices(compendiumSlot.PersonaId, out PersonaChoiceViewState compendiumPersona);

            return string.Join(
                Environment.NewLine,
                [
                    $"Compendium slot: {selectedCompendiumSlotIndex.Value}",
                    $"Persona: {compendiumPersona.Name}",
                    $"Level: {compendiumSlot.Level}",
                    $"XP: {compendiumSlot.TotalExperience}",
                ]);
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
        SaveEditorViewModel.GetPersonaChoices(slot.PersonaId, out PersonaChoiceViewState selectedPersona);

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

    private static void NavigateToSection(FrameworkElement target) =>
        target.StartBringIntoView();

    private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        IReadOnlyList<string> diagnosticsText = ShellStateFormatter.GetDiagnosticsText(diagnostics);
        void UpdateDiagnostics()
        {
            diagnosticsItems.Clear();
            foreach (string diagnosticText in diagnosticsText)
            {
                diagnosticsItems.Add(diagnosticText);
            }
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            UpdateDiagnostics();
            return;
        }

        _ = DispatcherQueue.TryEnqueue(UpdateDiagnostics);
    }

    private void SetUiDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        uiDiagnosticsOverride = diagnostics;
        DisplayDiagnostics(diagnostics);
    }

    private void RefreshPersonaDraftDiagnostics()
    {
        if (uiDiagnosticsOverride is null || !uiDiagnosticsOverride.Any(IsPersonaDraftDiagnostic))
        {
            return;
        }

        List<SaveDiagnostic> diagnostics = uiDiagnosticsOverride
            .Where(static diagnostic => !IsPersonaDraftDiagnostic(diagnostic))
            .ToList();

        if (!TryBuildPersonaSlotEdit(out PersonaSlotEdit personaSlotEdit, out SaveDiagnostic personaDiagnostic))
        {
            diagnostics.Add(personaDiagnostic);
        }
        else if (selectedCompendiumSlotIndex.HasValue &&
            (uint)selectedCompendiumSlotIndex.Value < (uint)viewModel.CompendiumPersonaSlots.Count)
        {
            PersonaSlotViewState currentCompendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
            personaSlotEdit = PreserveCompendiumPersonaIdentity(currentCompendiumSlot, personaSlotEdit);
            if (!TryValidateCompendiumPersonaExperienceChange(currentCompendiumSlot, personaSlotEdit, out SaveDiagnostic experienceDiagnostic))
            {
                diagnostics.Add(experienceDiagnostic);
            }
        }

        if (diagnostics.Count == 0)
        {
            uiDiagnosticsOverride = null;
            DisplayDiagnostics(viewModel.Diagnostics);
            return;
        }

        uiDiagnosticsOverride = Array.AsReadOnly(diagnostics.ToArray());
        DisplayDiagnostics(uiDiagnosticsOverride);
    }

    private static bool IsPersonaDraftDiagnostic(SaveDiagnostic diagnostic) =>
        diagnostic.Code is "P4GWINUI014" or "P4GWINUI015" or "P4GWINUI016" or "P4GWINUI031" or "P4GWINUI032" ||
        diagnostic.Target is "Persona.Xp" or "Persona.Skills" or "Persona.Level";

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

    private async Task ReportOpenFailureAsync(string source, string message)
    {
        IReadOnlyList<SaveDiagnostic> diagnostics =
        [
            CreateUiDiagnostic("P4GWINUI002", message, source),
        ];
        SetUiDiagnostics(diagnostics);
        await ShowMessageAsync("Open failed", FormatDiagnostics(diagnostics));
    }

    private async Task ShowAboutDialogAsync()
    {
        AboutDialog dialog = new();
        if (Content is FrameworkElement root)
        {
            dialog.XamlRoot = root.XamlRoot;
        }

        await dialog.ShowAsync();
    }

    private void UpdateWindowTitle()
    {
        Title = ShellStateFormatter.GetWindowTitle(currentFilePath);
    }

    internal static void ApplyOpenResult(bool succeeded, Action refreshEditorState, Action preserveEditorState)
    {
        ArgumentNullException.ThrowIfNull(refreshEditorState);
        ArgumentNullException.ThrowIfNull(preserveEditorState);

        if (succeeded)
        {
            refreshEditorState();
            return;
        }

        preserveEditorState();
    }

    internal static string? ConsumeStartupOpenPath(ref string? startupOpenPath)
    {
        if (string.IsNullOrWhiteSpace(startupOpenPath))
        {
            return null;
        }

        string openPath = startupOpenPath;
        startupOpenPath = null;
        return openPath;
    }

    internal static bool TryBeginBusyOperation(ref bool isBusy)
    {
        if (isBusy)
        {
            return false;
        }

        isBusy = true;
        return true;
    }

    internal static void EndBusyOperation(ref bool isBusy) =>
        isBusy = false;

    private static SaveDiagnostic CreateUiDiagnostic(string code, string message, string target) =>
        new(DiagnosticSeverity.Error, code, message, target);

    private static bool IsPersistenceException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string FormatPersonaSlot(PersonaSlotViewState slot) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Slot {slot.SlotIndex}: exists={slot.Exists}, id={slot.PersonaId}, level={slot.Level}, exp={slot.TotalExperience}");

    private static void TraceStartup(string message)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("P4G_TRACE_STARTUP"), "1", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "startup-trace.log"),
                $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

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
