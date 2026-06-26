using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private enum BusyOperationCompletion
    {
        RefreshViewModel,
        PreserveEditorState,
    }

    private readonly SaveEditorViewModel viewModel;
    private readonly InventorySelectionState inventorySelectionState = new();
    private readonly SaveEditorRefreshCoordinator saveEditorRefreshCoordinator = new();
    private string? startupOpenPath;
    private IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride;
    private string? currentFilePath;
    private bool isBusy;
    private bool suppressInventoryEvents;
    private bool suppressEquipmentEvents;
    private bool suppressPersonaEvents;
    private bool suppressCompendiumEvents;
    private bool suppressSocialLinkEvents;
    private bool preserveEditorTextDuringInventoryRefresh;
    private bool preservePersonaEditorStateDuringEquipmentRefresh;
    private bool autoSelectInventoryEntryAfterOpen;
    private bool autoSelectCompendiumEntryAfterOpen;
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

    private async void About_Click(object sender, RoutedEventArgs e) =>
        await ShowAboutDialogAsync();

    internal readonly record struct SocialLinkDraftState(
        int SlotIndex,
        byte LinkId,
        string LevelText,
        string ProgressText,
        string FlagText);

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
        picker.FileTypeFilter.Add(".sav");
        picker.FileTypeFilter.Add(".dat");
        picker.FileTypeFilter.Add("*");
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

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path);
            uiDiagnosticsOverride = null;
            SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.OpenSave(bytes));
            ApplyOpenResult(
                result.Succeeded,
                () =>
                {
                    currentFilePath = path;
                    selectedInventoryCategoryId = null;
                    selectedInventoryItemId = null;
                    selectedInventoryEntryId = null;
                    selectedEquipmentCharacterId = null;
                    selectedCompendiumSlotIndex = null;
                    selectedSocialLinkIndex = null;
                    selectedSocialLinkLinkId = null;
                    selectedPersonaMemberId = 0;
                    selectedPersonaSlotIndex = 0;
                    inventorySelectionState.Reset();
                    autoSelectInventoryEntryAfterOpen = true;
                    autoSelectCompendiumEntryAfterOpen = true;
                    InventoryQuantityTextBox.Text = string.Empty;
                    RefreshFromViewModel();
                },
                UpdateShellState);

            if (!result.Succeeded)
            {
                await ShowMessageAsync("Open failed", FormatDiagnostics(result.Diagnostics));
            }
        }
        catch (Exception ex) when (IsPersistenceException(ex))
        {
            await ReportOpenFailureAsync(source, $"Could not read the selected file: {ex.Message}");
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
            preserveSelectedSocialLinkDraft: ShouldPreserveSelectedSocialLinkDraftAfterApply(edits),
            preserveSelectedCompendiumDraft: ShouldPreserveSelectedCompendiumDraftAfterApply(edits));
        return true;
    }

    private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)
    {
        string? targetPath;
        bool blankSaveLoaded = false;
        if (forcePicker && !viewModel.HasSave)
        {
            targetPath = await PickSavePathAsync();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return BusyOperationCompletion.PreserveEditorState;
            }

            uiDiagnosticsOverride = null;
            SaveEditorOperationResult blankSaveResult = viewModel.CreateBlankSave();
            if (!blankSaveResult.Succeeded)
            {
                DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
                await ShowMessageAsync("Save failed", FormatDiagnostics(blankSaveResult.Diagnostics));
                return BusyOperationCompletion.PreserveEditorState;
            }

            blankSaveLoaded = true;
        }
        else
        {
            if (!ApplyEditorFields())
            {
                return BusyOperationCompletion.PreserveEditorState;
            }

            targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)
                ? await PickSavePathAsync()
                : currentFilePath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return BusyOperationCompletion.PreserveEditorState;
            }
        }

        uiDiagnosticsOverride = null;
        SaveEditorWriteResult writeResult = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.WriteSave());
        if (!writeResult.Succeeded || writeResult.Bytes is null || writeResult.OperationToken is null)
        {
            if (blankSaveLoaded)
            {
                RestoreNoSaveStateAfterFailedBlankSaveCore(
                    saveEditorRefreshCoordinator,
                    viewModel,
                    writeResult.Diagnostics,
                    RefreshFromViewModel,
                    ref uiDiagnosticsOverride);
            }
            else
            {
                RefreshFromViewModelPreservingInventoryQuantityDraft();
            }

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
            if (blankSaveLoaded)
            {
                RestoreNoSaveStateAfterFailedBlankSaveCore(
                    saveEditorRefreshCoordinator,
                    viewModel,
                    reportResult.Diagnostics,
                    RefreshFromViewModel,
                    ref uiDiagnosticsOverride);
            }
            else
            {
                RefreshFromViewModelPreservingInventoryQuantityDraft();
            }

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

    private void RestoreNoSaveStateAfterFailedBlankSave(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        uiDiagnosticsOverride = diagnostics;
        saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(() => viewModel.ClearSave());
        RefreshFromViewModel();
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowTitle();

        string? openPath = ConsumeStartupOpenPath(ref startupOpenPath);
        if (openPath is null)
        {
            return;
        }

        await RunBusyAsync(() => OpenSaveFileFromPathAsync(openPath, "Launch"));
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

    private async Task<DataPackageOperation> EvaluateDragOverAcceptanceAsync(DataPackageView dataView)
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
        AddPartyMemberValue(PartySlot0TextBox, 0, batch, validationDiagnostics);
        AddPartyMemberValue(PartySlot1TextBox, 1, batch, validationDiagnostics);
        AddPartyMemberValue(PartySlot2TextBox, 2, batch, validationDiagnostics);
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

        return TryFinalizeEditBatch(batch, validationDiagnostics, out edits, out diagnostics);
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
        if (selectedCompendiumSlotIndex.HasValue)
        {
            if (!TryBuildPersonaSlotEdit(out PersonaSlotEdit compendiumPersonaSlotEdit, out SaveDiagnostic compendiumDiagnostic))
            {
                diagnostics.Add(compendiumDiagnostic);
                return;
            }

            PersonaSlotViewState currentCompendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
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

    private bool TryAppendSelectedSocialLinkEdits(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return TryBuildSocialLinkEdits(
            selectedSocialLinkIndex,
            SocialLinkLevelTextBox.Text ?? string.Empty,
            SocialLinkProgressTextBox.Text ?? string.Empty,
            SocialLinkFlagTextBox.Text ?? string.Empty,
            edits,
            diagnostics);
    }

    internal static SaveEditorOperationResult RefreshSocialLinkDraftPreservingSelection(
        Func<SocialLinkDraftState?> captureDraft,
        Func<SaveEditorOperationResult> mutateSocialLinks,
        Action refreshSocialLinksState,
        Func<SocialLinkViewState?> selectedLinkProvider,
        Action<SocialLinkDraftState> restoreDraft)
    {
        ArgumentNullException.ThrowIfNull(captureDraft);
        ArgumentNullException.ThrowIfNull(mutateSocialLinks);
        ArgumentNullException.ThrowIfNull(refreshSocialLinksState);
        ArgumentNullException.ThrowIfNull(selectedLinkProvider);
        ArgumentNullException.ThrowIfNull(restoreDraft);

        SocialLinkDraftState? socialLinkDraft = captureDraft();
        SaveEditorOperationResult result = mutateSocialLinks();
        refreshSocialLinksState();
        if (socialLinkDraft is not null && ShouldRestoreSelectedSocialLinkDraft(socialLinkDraft.Value, selectedLinkProvider()))
        {
            restoreDraft(socialLinkDraft.Value);
        }
        return result;
    }

    internal static SocialLinkViewState? ResolveSelectedSocialLinkViewState(
        IReadOnlyList<SocialLinkViewState> socialLinks,
        int? selectedSocialLinkIndex,
        byte? selectedSocialLinkLinkId)
    {
        ArgumentNullException.ThrowIfNull(socialLinks);

        if (socialLinks.Count == 0)
        {
            return null;
        }

        if (selectedSocialLinkLinkId.HasValue)
        {
            SocialLinkViewState? selectedLink = socialLinks.FirstOrDefault(link => link.LinkId == selectedSocialLinkLinkId.Value);
            if (selectedLink is not null)
            {
                return selectedLink;
            }
        }

        if (selectedSocialLinkIndex.HasValue)
        {
            SocialLinkViewState? selectedLink = socialLinks.FirstOrDefault(link => link.SlotIndex == selectedSocialLinkIndex.Value);
            if (selectedLink is not null)
            {
                return selectedLink;
            }
        }

        return socialLinks[0];
    }

    internal static void ResetSelectedSocialLinkState(ref int? selectedSocialLinkIndex, ref byte? selectedSocialLinkLinkId)
    {
        selectedSocialLinkIndex = null;
        selectedSocialLinkLinkId = null;
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
            SocialLinkProgressTextBox.Text ?? string.Empty,
            SocialLinkFlagTextBox.Text ?? string.Empty);
    }

    private SocialLinkViewState? GetSelectedSocialLinkViewState() =>
        ResolveSelectedSocialLinkViewState(viewModel.SocialLinks, selectedSocialLinkIndex, selectedSocialLinkLinkId);

    private void RestoreSelectedSocialLinkDraft(SocialLinkDraftState socialLinkDraft)
    {
        if (!ShouldRestoreSelectedSocialLinkDraft(socialLinkDraft, GetSelectedSocialLinkViewState()))
        {
            return;
        }

        SocialLinkLevelTextBox.Text = socialLinkDraft.LevelText;
        SocialLinkProgressTextBox.Text = socialLinkDraft.ProgressText;
        SocialLinkFlagTextBox.Text = socialLinkDraft.FlagText;
    }

    internal static bool ShouldRestoreSelectedSocialLinkDraft(
        SocialLinkDraftState socialLinkDraft,
        SocialLinkViewState? selectedLink) =>
        selectedLink is not null &&
        selectedLink.LinkId == socialLinkDraft.LinkId;

    private CompendiumDraftState? CaptureSelectedCompendiumDraft()
    {
        if (!selectedCompendiumSlotIndex.HasValue)
        {
            return null;
        }

        ushort selectedPersonaId = PersonaChoiceComboBox.SelectedItem is PersonaChoiceViewState selectedChoice
            ? selectedChoice.PersonaId
            : viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value].PersonaId;

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
            PersonaChoiceComboBox.ItemsSource = viewModel.GetPersonaChoices(compendiumDraft.PersonaId, out PersonaChoiceViewState selectedCompendiumChoice);
            PersonaChoiceComboBox.SelectedItem = selectedCompendiumChoice;
            PersonaXpTextBox.Text = compendiumDraft.ExperienceText;
            PersonaLevelSlider.Value = compendiumDraft.Level;
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

    internal static bool TryBuildSocialLinkEdits(
        int? selectedSocialLinkIndex,
        string levelText,
        string progressText,
        string flagText,
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
        bool flagIsValid = TryReadSocialLinkField(flagText, "Flag", "SocialLinks.Flag", diagnostics, out byte flag);
        if (!levelIsValid || !progressIsValid || !flagIsValid)
        {
            return false;
        }

        edits.Add(new SetSocialLinkLevelEdit(selectedSocialLinkIndex.Value, level));
        edits.Add(new SetSocialLinkProgressEdit(selectedSocialLinkIndex.Value, progress));
        edits.Add(new SetSocialLinkFlagEdit(selectedSocialLinkIndex.Value, flag));
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
        out SaveDiagnostic diagnostic)
    {
        personaSlotEdit = new PersonaSlotEdit(0, 0, 0, Array.Empty<ushort>(), 0, 0, 0, 0, 0);
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");

        if (!TryReadSelectedPersonaId(out ushort personaId, out diagnostic) ||
            !TryReadSelectedSkillIds(out IReadOnlyList<ushort> skillIds, out diagnostic))
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
            out diagnostic);
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
            if (slot.Exists && slot.PersonaId == personaId)
            {
                slotIndex = slot.SlotIndex;
                existingSlot = true;
                return true;
            }
        }

        for (int index = 0; index < compendiumPersonaSlots.Count; index++)
        {
            PersonaSlotViewState slot = compendiumPersonaSlots[index];
            if (!slot.Exists)
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
        out SaveDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(skillIds);

        personaSlotEdit = new PersonaSlotEdit(0, 0, 0, Array.Empty<ushort>(), 0, 0, 0, 0, 0);
        diagnostic = CreateUiDiagnostic("P4GWINUI014", "Persona edit could not be built.", "Persona");

        if (!uint.TryParse(totalExperienceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint totalExperience))
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI015", "Persona total experience must be an unsigned whole number.", "Persona.Xp");
            return false;
        }

        if (skillIds.Any(static skillId => skillId == ushort.MaxValue))
        {
            diagnostic = CreateUiDiagnostic("P4GWINUI016", "Select a skill for each persona slot.", "Persona.Skills");
            return false;
        }

        personaSlotEdit = new PersonaSlotEdit(
            personaId,
            (byte)Math.Round(level, MidpointRounding.AwayFromZero),
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
            [0, 0, 0, 0, 0, 0, 0, 0],
            1,
            1,
            1,
            1,
            1);

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
        RefreshEditableFields();
        RefreshInventoryState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
    }

    private void RefreshFromViewModelPreservingInventoryQuantityDraft(
        bool preserveSelectedSocialLinkDraft = true,
        bool preserveSelectedCompendiumDraft = true)
    {
        byte? selectedInventoryCategoryIdBeforeRefresh = selectedInventoryCategoryId;
        ushort? selectedInventoryItemIdBeforeRefresh = selectedInventoryItemId;
        ushort? selectedInventoryEntryIdBeforeRefresh = selectedInventoryEntryId;
        string inventoryQuantityDraft = InventoryQuantityTextBox.Text;
        SocialLinkDraftState? socialLinkDraft = CaptureSelectedSocialLinkDraft();
        CompendiumDraftState? compendiumDraft = preserveSelectedCompendiumDraft ? CaptureSelectedCompendiumDraft() : null;

        RefreshFromViewModel();

        if (InventorySelectionState.ShouldRestoreQuantityDraft(
                selectedInventoryCategoryIdBeforeRefresh,
                selectedInventoryItemIdBeforeRefresh,
                selectedInventoryEntryIdBeforeRefresh,
                selectedInventoryCategoryId,
                selectedInventoryItemId,
                selectedInventoryEntryId))
        {
            InventoryQuantityTextBox.Text = inventoryQuantityDraft;
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

    internal static bool ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(
        int? selectedCompendiumSlotIndexBeforeMutation,
        int? selectedCompendiumSlotIndexAfterMutation,
        bool mutationSucceeded) =>
        !mutationSucceeded ||
        selectedCompendiumSlotIndexBeforeMutation == selectedCompendiumSlotIndexAfterMutation;

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
        FamilyNameTextBox.Text = viewModel.FamilyName;
        GivenNameTextBox.Text = viewModel.GivenName;
        YenTextBox.Text = viewModel.HasSave ? viewModel.Yen.ToString(CultureInfo.InvariantCulture) : string.Empty;
        RefreshSocialStatsState();
        RefreshCalendarState();
        RefreshSocialLinksState();
        RefreshCompendiumState();
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
        bool canSaveAs = !isBusy;

        FileOpenMenuItem.IsEnabled = !isBusy;
        OpenButton.IsEnabled = !isBusy;
        ApplyButton.IsEnabled = canEdit;
        SaveButton.IsEnabled = canSave;
        SaveAsButton.IsEnabled = canSaveAs;
        FileSaveMenuItem.IsEnabled = canSave;
        FileSaveAsMenuItem.IsEnabled = canSaveAs;
        FamilyNameTextBox.IsEnabled = canEdit;
        GivenNameTextBox.IsEnabled = canEdit;
        YenTextBox.IsEnabled = canEdit;
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
        SocialLinkFlagTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        SocialLinkApplyButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        SocialLinkDeleteButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        CompendiumListView.IsEnabled = canEdit;
        CompendiumAddComboBox.IsEnabled = canEdit;
        CompendiumRemoveButton.IsEnabled = canEdit && selectedCompendiumSlotIndex.HasValue;
        CompendiumClearButton.IsEnabled = canEdit;
        PartySlot0TextBox.IsEnabled = canEdit;
        PartySlot1TextBox.IsEnabled = canEdit;
        PartySlot2TextBox.IsEnabled = canEdit;
        PersonaMemberComboBox.IsEnabled = canEdit;
        PersonaSlotComboBox.IsEnabled = canEdit && selectedPersonaMemberId == 0 && !selectedCompendiumSlotIndex.HasValue;
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

        FilePathTextBlock.Text = ShellStateFormatter.GetFilePathText(currentFilePath);
        StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, viewModel.IsDirty, viewModel.CanWrite);
        UpdateWindowTitle();
    }

    private void RefreshSocialStatsState()
    {
        if (!viewModel.HasSave || viewModel.SocialStats.Count == 0)
        {
            CourageComboBox.ItemsSource = Array.Empty<SocialStatRankChoiceViewState>();
            KnowledgeComboBox.ItemsSource = Array.Empty<SocialStatRankChoiceViewState>();
            ExpressionComboBox.ItemsSource = Array.Empty<SocialStatRankChoiceViewState>();
            UnderstandingComboBox.ItemsSource = Array.Empty<SocialStatRankChoiceViewState>();
            DiligenceComboBox.ItemsSource = Array.Empty<SocialStatRankChoiceViewState>();
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
    }

    private void SetSocialStatSelection(ComboBox comboBox, int statIndex)
    {
        SocialStatViewState stat = viewModel.SocialStats[statIndex];
        comboBox.ItemsSource = viewModel.GetSocialStatChoices(statIndex, stat.Points, out SocialStatRankChoiceViewState selectedChoice);
        comboBox.SelectedItem = selectedChoice;
    }

    private void RefreshCalendarState()
    {
        if (!viewModel.HasSave)
        {
            PhaseComboBox.ItemsSource = Array.Empty<CalendarPhaseChoiceViewState>();
            NextPhaseComboBox.ItemsSource = Array.Empty<CalendarPhaseChoiceViewState>();
            PhaseComboBox.SelectedItem = null;
            NextPhaseComboBox.SelectedItem = null;
            DayTextBox.Text = string.Empty;
            NextDayTextBox.Text = string.Empty;
            return;
        }

        DayTextBox.Text = viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture);
        NextDayTextBox.Text = viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture);
        PhaseComboBox.ItemsSource = viewModel.GetCalendarPhaseChoices(viewModel.Calendar.DayPhaseId, out CalendarPhaseChoiceViewState selectedPhase);
        PhaseComboBox.SelectedItem = selectedPhase;
        NextPhaseComboBox.ItemsSource = viewModel.GetCalendarPhaseChoices(viewModel.Calendar.NextDayPhaseId, out CalendarPhaseChoiceViewState selectedNextPhase);
        NextPhaseComboBox.SelectedItem = selectedNextPhase;
    }

    private void RefreshSocialLinksState()
    {
        suppressSocialLinkEvents = true;
        try
        {
            SocialLinkListView.ItemsSource = viewModel.HasSave
                ? viewModel.SocialLinks
                : Array.Empty<SocialLinkViewState>();

            IReadOnlyList<SocialLinkChoiceViewState> linkChoices = viewModel.GetSocialLinkChoices(0, out SocialLinkChoiceViewState blankChoice);
            SocialLinkAddComboBox.ItemsSource = viewModel.HasSave
                ? linkChoices
                : Array.Empty<SocialLinkChoiceViewState>();

            if (!viewModel.HasSave || viewModel.SocialLinks.Count == 0)
            {
                ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);
                SocialLinkListView.SelectedItem = null;
                SocialLinkAddComboBox.SelectedItem = viewModel.HasSave ? blankChoice : null;
                SocialLinkLevelTextBox.Text = string.Empty;
                SocialLinkProgressTextBox.Text = string.Empty;
                SocialLinkFlagTextBox.Text = string.Empty;
                return;
            }

            SocialLinkViewState selectedLink = ResolveSelectedSocialLinkViewState(viewModel.SocialLinks, selectedSocialLinkIndex, selectedSocialLinkLinkId)
                ?? viewModel.SocialLinks[0];
            selectedSocialLinkIndex = selectedLink.SlotIndex;
            selectedSocialLinkLinkId = selectedLink.LinkId;

            SocialLinkListView.SelectedItem = selectedLink;
            SocialLinkAddComboBox.SelectedItem = blankChoice;
            SocialLinkLevelTextBox.Text = selectedLink.Level.ToString(CultureInfo.InvariantCulture);
            SocialLinkProgressTextBox.Text = selectedLink.Progress.ToString(CultureInfo.InvariantCulture);
            SocialLinkFlagTextBox.Text = selectedLink.Flag.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            suppressSocialLinkEvents = false;
        }
    }

    private void RefreshCompendiumState()
    {
        suppressCompendiumEvents = true;
        try
        {
            IReadOnlyList<CompendiumPersonaViewState> compendiumEntries = [];
            PersonaChoiceViewState? blankChoice = null;
            if (viewModel.HasSave)
            {
                compendiumEntries = viewModel.CompendiumPersonaSlots
                    .Where(static slot => slot.Exists)
                    .Select(slot =>
                    {
                        viewModel.GetPersonaChoices(slot.PersonaId, out PersonaChoiceViewState choice);
                        return new CompendiumPersonaViewState(slot.SlotIndex, slot.PersonaId, choice.Name, slot.Level, slot.TotalExperience);
                    })
                    .ToArray();
                viewModel.GetPersonaChoices(0, out blankChoice);
            }

            CompendiumListView.ItemsSource = compendiumEntries;
            IReadOnlyList<PersonaChoiceViewState> addChoices = viewModel.HasSave
                ? viewModel.GetPersonaChoices(0, out blankChoice)
                : [];
            CompendiumAddComboBox.ItemsSource = addChoices;
            CompendiumAddComboBox.SelectedItem = viewModel.HasSave ? blankChoice : null;

            if (!viewModel.HasSave || compendiumEntries.Count == 0)
            {
                selectedCompendiumSlotIndex = null;
                CompendiumListView.SelectedItem = null;
                autoSelectCompendiumEntryAfterOpen = false;
                return;
            }

            CompendiumPersonaViewState? selectedEntry = ResolveSelectedCompendiumViewState(
                compendiumEntries,
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

    private void SocialLinkListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSocialLinkEvents)
        {
            return;
        }

        if (SocialLinkListView.SelectedItem is not SocialLinkViewState selectedLink)
        {
            selectedSocialLinkIndex = null;
            selectedSocialLinkLinkId = null;
            RefreshSocialLinksState();
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

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = RefreshSocialLinkDraftPreservingSelection(
            CaptureSelectedSocialLinkDraft,
            () =>
            {
                SaveEditorOperationResult mutationResult = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                    () => viewModel.AddSocialLink(selectedChoice.LinkId));
                if (mutationResult.Succeeded && viewModel.SocialLinks.Count > 0)
                {
                    SocialLinkViewState selectedLink = viewModel.SocialLinks.Last();
                    selectedSocialLinkIndex = selectedLink.SlotIndex;
                    selectedSocialLinkLinkId = selectedLink.LinkId;
                }

                return mutationResult;
            },
            RefreshSocialLinksState,
            GetSelectedSocialLinkViewState,
            RestoreSelectedSocialLinkDraft);
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Social link add failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void SocialLinkApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI022", "Open a save before editing social links.", "SocialLinks")]);
            return;
        }

        if (!selectedSocialLinkIndex.HasValue)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI023", "Select a social link before applying edits.", "SocialLinks.Item")]);
            return;
        }

        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> validationDiagnostics = [];
        if (!TryAppendSelectedSocialLinkEdits(edits, validationDiagnostics))
        {
            SetUiDiagnostics(validationDiagnostics);
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => viewModel.ApplyEdits(edits));
        RefreshSocialLinksState();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Social link update failed", FormatDiagnostics(result.Diagnostics));
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

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = RefreshSocialLinkDraftPreservingSelection(
            CaptureSelectedSocialLinkDraft,
            () =>
            {
                SaveEditorOperationResult mutationResult = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                    () => viewModel.RemoveSocialLink(selectedSocialLinkIndex.Value));
                if (mutationResult.Succeeded)
                {
                    selectedSocialLinkIndex = viewModel.SocialLinks.Count == 0
                        ? null
                        : Math.Min(selectedSocialLinkIndex.Value, viewModel.SocialLinks.Count - 1);
                }

                return mutationResult;
            },
            RefreshSocialLinksState,
            GetSelectedSocialLinkViewState,
            RestoreSelectedSocialLinkDraft);
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
        SaveEditorOperationResult result = saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
            () => SelectOrAddCompendiumPersona(selectedChoice));
        RefreshFromViewModelPreservingInventoryQuantityDraft(
            preserveSelectedCompendiumDraft: ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(
                selectedCompendiumSlotIndexBeforeMutation,
                selectedCompendiumSlotIndex,
                result.Succeeded));
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

            if (selectedCompendiumSlotIndex.HasValue)
            {
                PersonaSlotViewState currentCompendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
                (selectedPersonaMemberId, selectedPersonaSlotIndex) =
                    PreserveSelectedPersonaSelectionDuringCompendiumRefresh(selectedPersonaMemberId, selectedPersonaSlotIndex);
                PersonaMemberComboBox.SelectedItem = null;
                PersonaSlotComboBox.ItemsSource = Array.Empty<PersonaSlotViewState>();
                PersonaSlotComboBox.SelectedItem = null;
                PersonaChoiceComboBox.ItemsSource = viewModel.GetPersonaChoices(
                    currentCompendiumSlot.PersonaId,
                    out PersonaChoiceViewState selectedCompendiumChoice);
                PersonaChoiceComboBox.SelectedItem = selectedCompendiumChoice;
                PersonaXpTextBox.Text = currentCompendiumSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
                PersonaLevelSlider.Value = currentCompendiumSlot.Level;
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
                PersonaSlotComboBox.ItemsSource = Array.Empty<PersonaSlotViewState>();
                PersonaSlotComboBox.SelectedItem = null;
                return;
            }

            if (isProtagonist)
            {
                selectedPersonaSlotIndex = ResolveSelectedPersonaSlotIndexForProtagonistView(
                    selectedPersonaSlotIndex,
                    personaSlots);
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

        if (selectedCompendiumSlotIndex.HasValue)
        {
            ClearSelectedCompendiumContext(ref selectedCompendiumSlotIndex);
            CompendiumListView.SelectedItem = null;
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

        if (selectedCompendiumSlotIndex.HasValue)
        {
            PersonaSlotViewState compendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
            viewModel.GetPersonaChoices(compendiumSlot.PersonaId, out PersonaChoiceViewState compendiumPersona);

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
        DiagnosticsListView.ItemsSource = ShellStateFormatter.GetDiagnosticsText(diagnostics);
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

    internal static void RestoreNoSaveStateAfterFailedBlankSaveCore(
        SaveEditorRefreshCoordinator refreshCoordinator,
        SaveEditorViewModel viewModel,
        IReadOnlyList<SaveDiagnostic> diagnostics,
        Action refreshFromViewModel,
        ref IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride)
    {
        ArgumentNullException.ThrowIfNull(refreshCoordinator);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(refreshFromViewModel);

        uiDiagnosticsOverride = diagnostics;
        refreshCoordinator.RunWithFullRefreshSuppressed(() => viewModel.ClearSave());
        refreshFromViewModel();
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
