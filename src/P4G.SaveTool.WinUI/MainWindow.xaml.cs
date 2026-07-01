using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Application;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace P4G.SaveTool.WinUI;

public sealed partial class MainWindow : Window
{
    private const uint LegacyCompendiumMaximumTotalExperience = 999_999_999;
    private const int DefaultWindowWidthDip = 1180;
    private const int DefaultWindowHeightDip = 820;

    private enum BusyOperationCompletion
    {
        RefreshViewModel,
        PreserveEditorState,
    }

    private readonly SaveEditorViewModel viewModel;
    private readonly ObservableCollection<DiagnosticListItemViewState> diagnosticsItems = new();
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
    private bool isWorkspaceRoutingInitialized;
    private bool basicStatsWorkspacePageInitializedFromViewModel;
    private bool calendarSocialStatsWorkspacePageInitializedFromViewModel;
    private bool preserveEditorTextDuringInventoryRefresh;
    private bool autoSelectInventoryEntryAfterOpen;
    private bool autoSelectCompendiumEntryAfterOpen;
    private bool refreshEditableFieldsAfterStartupOpen;
    private bool inventoryQuantityDraftDirty;
    private byte? selectedInventoryCategoryId;
    private ushort? selectedInventoryItemId;
    private ushort? selectedInventoryEntryId;
    private byte? selectedEquipmentCharacterId;
    private int? selectedCompendiumListSlotIndex;
    private int? selectedCompendiumSlotIndex;
    private int? selectedSocialLinkIndex;
    private byte? selectedSocialLinkLinkId;
    private byte? selectedPersonaMemberId;
    private int selectedPersonaSlotIndex;
    private BasicStatsWorkspacePage? basicStatsWorkspacePage;
    private CalendarSocialStatsWorkspacePage? calendarSocialStatsWorkspacePage;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow(string? startupOpenPath = null)
    {
        this.startupOpenPath = startupOpenPath;
        InitializeComponent();
        ResizeToDefaultMultiPaneSize();
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
        defaultPersonaLevelValueForeground = PersonaLevelValueTextBlock.Foreground;
        SectionNavigationView.SelectedItem = JumpOverviewButton;
    }

    private void ResizeToDefaultMultiPaneSize()
    {
        IntPtr hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32(
            (int)Math.Ceiling(DefaultWindowWidthDip * scale),
            (int)Math.Ceiling(DefaultWindowHeightDip * scale)));
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(OpenSaveFileAsync);

    private async void ApplyButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(
            () =>
            {
                _ = ApplyEditorFields();
                return Task.FromResult(BusyOperationCompletion.PreserveEditorState);
            });

    private async void SaveButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(() => SaveAsync(forcePicker: false));

    private async void SaveAsButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(() => SaveAsync(forcePicker: true));

    private async void About_Click(object sender, RoutedEventArgs e) =>
        await ShowAboutDialogAsync();

    private void MainCharacterLevelSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateMainCharacterLevelValueText();
        TrackEditorDraft();
    }

    private void SectionNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem selectedItem ||
            selectedItem.Tag is not string sectionTag)
        {
            return;
        }

        NavigateToWorkspace(sectionTag);
    }

    private void NavigateToWorkspace(string sectionTag)
    {
        if (!isWorkspaceRoutingInitialized)
        {
            return;
        }

        if (sectionTag == "Overview")
        {
            NavigateToOverviewWorkspace();
            return;
        }

        if (sectionTag == "DiagnosticsState")
        {
            NavigateToDiagnosticsWorkspace();
            return;
        }

        if (sectionTag == "BasicStats")
        {
            NavigateToBasicStatsWorkspace();
            return;
        }

        if (sectionTag == "CalendarSocialStats")
        {
            NavigateToCalendarSocialStatsWorkspace();
            return;
        }

        EnsureLegacyWorkspaceRouted();
        if (viewModel is not null && viewModel.HasSave)
        {
            NavigateToSelectedSection(sectionTag);
        }
    }

    private void EnsureLegacyWorkspaceRouted()
    {
        if (LegacyWorkspaceContentStore.Parent is Panel legacyParent)
        {
            legacyParent.Children.Remove(LegacyWorkspaceContentStore);
        }

        LegacyWorkspaceContentStore.Visibility = Visibility.Visible;

        if (WorkspaceFrame.Content is WorkspaceHostPage hostPage)
        {
            hostPage.SetWorkspaceContent(LegacyWorkspaceContentStore);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(WorkspaceHostPage), LegacyWorkspaceContentStore))
        {
            throw new InvalidOperationException("Could not navigate to the save editor workspace host page.");
        }

        WorkspaceFrame.BackStack.Clear();
    }

    private void NavigateToOverviewWorkspace()
    {
        LegacyWorkspaceContentStore.Visibility = Visibility.Collapsed;

        if (WorkspaceFrame.Content is OverviewWorkspacePage overviewPage)
        {
            ConfigureOverviewWorkspacePage(overviewPage, CreateOverviewWorkspaceState());
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(OverviewWorkspacePage), CreateOverviewWorkspaceState()))
        {
            throw new InvalidOperationException("Could not navigate to the overview workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is OverviewWorkspacePage navigatedOverviewPage)
        {
            ConfigureOverviewWorkspacePage(navigatedOverviewPage, CreateOverviewWorkspaceState());
        }
    }

    private OverviewWorkspaceViewState CreateOverviewWorkspaceState()
    {
        bool hasPendingEditorDrafts = HasPendingEditorDrafts();
        return new OverviewWorkspaceViewState(
            viewModel.HasSave,
            ShellStateFormatter.GetFilePathText(currentFilePath),
            ShellStateFormatter.GetStatusText(viewModel.HasSave, hasPendingEditorDrafts, viewModel.IsDirty, viewModel.CanWrite));
    }

    private void ConfigureOverviewWorkspacePage(OverviewWorkspacePage overviewPage, OverviewWorkspaceViewState overviewState)
    {
        overviewPage.OpenSaveRequested -= OverviewWorkspacePage_OpenSaveRequested;
        overviewPage.OpenSaveRequested += OverviewWorkspacePage_OpenSaveRequested;
        overviewPage.SetOverviewState(overviewState);
    }

    private async void OverviewWorkspacePage_OpenSaveRequested(object? sender, RoutedEventArgs e) =>
        await RunBusyAsync(OpenSaveFileAsync);

    private void NavigateToDiagnosticsWorkspace()
    {
        LegacyWorkspaceContentStore.Visibility = Visibility.Collapsed;

        if (WorkspaceFrame.Content is DiagnosticsWorkspacePage diagnosticsPage)
        {
            diagnosticsPage.SetDiagnosticsItems(diagnosticsItems);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(DiagnosticsWorkspacePage), diagnosticsItems))
        {
            throw new InvalidOperationException("Could not navigate to the diagnostics workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
    }

    private void NavigateToBasicStatsWorkspace()
    {
        LegacyWorkspaceContentStore.Visibility = Visibility.Collapsed;

        if (WorkspaceFrame.Content is BasicStatsWorkspacePage page)
        {
            ConfigureBasicStatsWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(BasicStatsWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the basic stats workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is BasicStatsWorkspacePage navigatedPage)
        {
            ConfigureBasicStatsWorkspacePage(navigatedPage);
        }
    }

    private void ConfigureBasicStatsWorkspacePage(BasicStatsWorkspacePage page)
    {
        basicStatsWorkspacePage = page;
        page.FamilyNameTextChanged -= FamilyNameTextBox_TextChanged;
        page.FamilyNameTextChanged += FamilyNameTextBox_TextChanged;
        page.GivenNameTextChanged -= GivenNameTextBox_TextChanged;
        page.GivenNameTextChanged += GivenNameTextBox_TextChanged;
        page.YenTextChanged -= YenTextBox_TextChanged;
        page.YenTextChanged += YenTextBox_TextChanged;
        page.MainCharacterLevelValueChanged -= MainCharacterLevelSlider_ValueChanged;
        page.MainCharacterLevelValueChanged += MainCharacterLevelSlider_ValueChanged;
        page.MainCharacterTotalExperienceTextChanged -= MainCharacterTotalExperienceTextBox_TextChanged;
        page.MainCharacterTotalExperienceTextChanged += MainCharacterTotalExperienceTextBox_TextChanged;
        page.MainCharacterCalculateFromLevelClick -= MainCharacterCalculateFromLevelButton_Click;
        page.MainCharacterCalculateFromLevelClick += MainCharacterCalculateFromLevelButton_Click;

        if (!basicStatsWorkspacePageInitializedFromViewModel)
        {
            RefreshBasicStatsState();
        }

        page.SetBasicStatsEnabled(CanEditBasicStats());
    }

    private void NavigateToCalendarSocialStatsWorkspace()
    {
        LegacyWorkspaceContentStore.Visibility = Visibility.Collapsed;

        if (WorkspaceFrame.Content is CalendarSocialStatsWorkspacePage page)
        {
            ConfigureCalendarSocialStatsWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(CalendarSocialStatsWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the calendar and social stats workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is CalendarSocialStatsWorkspacePage navigatedPage)
        {
            ConfigureCalendarSocialStatsWorkspacePage(navigatedPage);
        }
    }

    private void ConfigureCalendarSocialStatsWorkspacePage(CalendarSocialStatsWorkspacePage page)
    {
        calendarSocialStatsWorkspacePage = page;
        page.SocialStatSelectionChanged -= CalendarSocialStatsWorkspacePage_SocialStatSelectionChanged;
        page.SocialStatSelectionChanged += CalendarSocialStatsWorkspacePage_SocialStatSelectionChanged;
        page.DayTextChanged -= CalendarSocialStatsWorkspacePage_DayTextChanged;
        page.DayTextChanged += CalendarSocialStatsWorkspacePage_DayTextChanged;
        page.PhaseSelectionChanged -= CalendarSocialStatsWorkspacePage_PhaseSelectionChanged;
        page.PhaseSelectionChanged += CalendarSocialStatsWorkspacePage_PhaseSelectionChanged;

        if (!calendarSocialStatsWorkspacePageInitializedFromViewModel)
        {
            RefreshSocialStatsState();
            RefreshCalendarState();
        }

        page.SetCalendarSocialStatsEnabled(CanEditCalendarSocialStats());
    }

    private void WorkspaceFrame_NavigationFailed(object sender, NavigationFailedEventArgs e) =>
        throw new InvalidOperationException($"Could not navigate to workspace page {e.SourcePageType.FullName}.", e.Exception);

    private void NavigateToSelectedSection(string sectionTag)
    {
        switch (sectionTag)
        {
            case "SocialLinks":
                NavigateToSection(SocialLinksSectionHeader);
                break;
            case "PartyPersona":
                NavigateToSection(PartyPersonaSectionHeader);
                break;
            case "Equipment":
                NavigateToSection(EquipmentSectionHeader);
                break;
            case "Compendium":
                NavigateToSection(CompendiumSectionHeader);
                break;
            case "Inventory":
                NavigateToSection(InventorySectionHeader);
                break;
        }
    }

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
        NavigateToWorkspace("DiagnosticsState");

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

    private void MainCharacterCalculateFromLevelButton_Click(object sender, RoutedEventArgs e)
    {
        if (basicStatsWorkspacePage is null)
        {
            return;
        }

        basicStatsWorkspacePage.MainCharacterTotalExperienceText =
            LevelExperienceProjection.CalculateTotalExperienceFromLevel((byte)basicStatsWorkspacePage.MainCharacterLevelRawValue)
                .ToString(CultureInfo.InvariantCulture);
    }

    private void UpdateMainCharacterLevelValueText()
    {
        if (basicStatsWorkspacePage is null)
        {
            return;
        }

        bool hasSave = viewModel is not null && viewModel.HasSave;
        double level = basicStatsWorkspacePage.MainCharacterLevelRawValue;
        basicStatsWorkspacePage.SetMainCharacterLevelValueText(hasSave
            ? ((byte)Math.Round(level, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
            : string.Empty);
        basicStatsWorkspacePage.SetMainCharacterLevelValueForeground(
            hasSave && IsLegacyLevelWarningValue(level)
                ? legacyLevelWarningForeground
                : basicStatsWorkspacePage.DefaultMainCharacterLevelValueForeground);
    }

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
        await Task.Yield();
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
                    selectedCompendiumListSlotIndex = null;
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

        if (!await ConfirmOverwriteSaveAsync(targetPath))
        {
            return BusyOperationCompletion.PreserveEditorState;
        }

        if (HasPendingEditorDrafts())
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI037", "Apply edits before saving.", "Save")]);
            UpdateShellState();
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
        isWorkspaceRoutingInitialized = true;
        if (SectionNavigationView.SelectedItem is NavigationViewItem selectedItem &&
            selectedItem.Tag is string sectionTag)
        {
            NavigateToWorkspace(sectionTag);
        }
        else
        {
            NavigateToOverviewWorkspace();
        }

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

        DragOperationDeferral deferral = e.GetDeferral();
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
        finally
        {
            deferral.Complete();
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

        string familyName = GetCurrentFamilyNameText();
        string givenName = GetCurrentGivenNameText();

        if (uint.TryParse(GetCurrentYenText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedYen))
        {
            batch.Add(new SetYenEdit(parsedYen));
        }
        else
        {
            validationDiagnostics.Add(CreateUiDiagnostic("P4GWINUI006", "Yen must be an unsigned whole number.", "Yen"));
        }

        batch.Add(new SetSaveNamesEdit(familyName, givenName));
        batch.Add(new SetMainCharacterLevelEdit((byte)GetCurrentMainCharacterLevelRawValue()));
        if (uint.TryParse(GetCurrentMainCharacterTotalExperienceText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedMainCharacterTotalExperience))
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
        AddSelectedEquipmentEdits(batch, validationDiagnostics);
        AppendGroup4Edits(
            viewModel.SocialStats,
            viewModel.Calendar,
            GetCurrentGroup4EditInputs(),
            batch,
            validationDiagnostics);
        AddPersonaEdit(batch, validationDiagnostics);
        TryAppendSelectedSocialLinkEdits(batch, validationDiagnostics);
        TryAppendSocialLinkAddEdit(batch, validationDiagnostics);
        TryAppendCompendiumAddEdit(batch, validationDiagnostics);
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

    private void AddSelectedEquipmentEdits(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        EquipmentCharacterViewState? selectedCharacter = GetSelectedEquipmentCharacterViewState();
        if (selectedCharacter is null)
        {
            return;
        }

        AddEquipmentEdit(
            EquipmentWeaponComboBox,
            selectedCharacter.WeaponItemId,
            "Equipment.Weapon",
            itemId => new SetEquippedWeaponEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
        AddEquipmentEdit(
            EquipmentArmorComboBox,
            selectedCharacter.ArmorItemId,
            "Equipment.Armor",
            itemId => new SetEquippedArmorEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
        AddEquipmentEdit(
            EquipmentAccessoryComboBox,
            selectedCharacter.AccessoryItemId,
            "Equipment.Accessory",
            itemId => new SetEquippedAccessoryEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
        AddEquipmentEdit(
            EquipmentCostumeComboBox,
            selectedCharacter.CostumeItemId,
            "Equipment.Costume",
            itemId => new SetEquippedCostumeEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
    }

    private static void AddEquipmentEdit(
        ComboBox comboBox,
        ushort currentItemId,
        string diagnosticTarget,
        Func<ushort, SaveEditCommand> createEdit,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (comboBox.SelectedItem is not InventoryItemChoiceViewState selectedItem)
        {
            diagnostics.Add(CreateUiDiagnostic("P4GWINUI038", "Select an equipment item.", diagnosticTarget));
            return;
        }

        if (selectedItem.ItemId != currentItemId)
        {
            edits.Add(createEdit(selectedItem.ItemId));
        }
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
            selectedCompendiumListSlotIndex = compendiumDraft.SlotIndex;
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

    private bool TryAppendSocialLinkAddEdit(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (SocialLinkAddComboBox.SelectedItem is not SocialLinkChoiceViewState selectedChoice ||
            selectedChoice.IsPlaceholder)
        {
            return true;
        }

        edits.Add(new AddSocialLinkEdit(selectedChoice.LinkId));
        return true;
    }

    private bool TryAppendCompendiumAddEdit(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (CompendiumAddComboBox.SelectedItem is not PersonaChoiceViewState selectedChoice ||
            selectedChoice.PersonaId == 0)
        {
            return true;
        }

        if (!TryResolveCompendiumPersonaAddTarget(
                viewModel.CompendiumPersonaSlots,
                selectedChoice.PersonaId,
                out int slotIndex,
                out bool existingSlot,
                out SaveDiagnostic? diagnostic))
        {
            diagnostics.Add(diagnostic!);
            return false;
        }

        if (!existingSlot)
        {
            edits.Add(new SetCompendiumPersonaSlotEdit(slotIndex, CreateDefaultCompendiumPersonaSlotEdit(selectedChoice.PersonaId)));
        }

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
        if (basicStatsWorkspacePage is null)
        {
            basicStatsWorkspacePageInitializedFromViewModel = false;
            return;
        }

        suppressImmediateEditEvents = true;
        try
        {
            basicStatsWorkspacePage.FamilyNameText = viewModel.FamilyName;
            basicStatsWorkspacePage.GivenNameText = viewModel.GivenName;
            basicStatsWorkspacePage.YenText = viewModel.HasSave ? viewModel.Yen.ToString(CultureInfo.InvariantCulture) : string.Empty;
            basicStatsWorkspacePage.SetMainCharacterLevelRawValue(viewModel.HasSave ? viewModel.MainCharacterLevel : 0);
            UpdateMainCharacterLevelValueText();
            basicStatsWorkspacePage.MainCharacterTotalExperienceText = viewModel.HasSave
                ? viewModel.MainCharacterTotalExperience.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            basicStatsWorkspacePageInitializedFromViewModel = true;
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
    }

    private bool CanEditBasicStats() =>
        viewModel.HasSave && !isBusy && !refreshEditableFieldsAfterStartupOpen;

    private bool CanEditCalendarSocialStats() =>
        viewModel.HasSave && !isBusy && !refreshEditableFieldsAfterStartupOpen;

    private string GetCurrentFamilyNameText() =>
        basicStatsWorkspacePage?.FamilyNameText ?? viewModel.FamilyName;

    private string GetCurrentGivenNameText() =>
        basicStatsWorkspacePage?.GivenNameText ?? viewModel.GivenName;

    private string GetCurrentYenText() =>
        basicStatsWorkspacePage?.YenText ??
        (viewModel.HasSave ? viewModel.Yen.ToString(CultureInfo.InvariantCulture) : string.Empty);

    private double GetCurrentMainCharacterLevelRawValue() =>
        basicStatsWorkspacePage?.MainCharacterLevelRawValue ?? (viewModel.HasSave ? viewModel.MainCharacterLevel : 0);

    private string GetCurrentMainCharacterTotalExperienceText() =>
        basicStatsWorkspacePage?.MainCharacterTotalExperienceText ??
        (viewModel.HasSave ? viewModel.MainCharacterTotalExperience.ToString(CultureInfo.InvariantCulture) : string.Empty);

    private Group4EditInputs GetCurrentGroup4EditInputs()
    {
        if (calendarSocialStatsWorkspacePage is not null &&
            calendarSocialStatsWorkspacePageInitializedFromViewModel)
        {
            return CreateGroup4EditInputs(
                calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(0),
                calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(1),
                calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(4),
                calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(3),
                calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(2),
                calendarSocialStatsWorkspacePage.DayText,
                calendarSocialStatsWorkspacePage.GetCalendarPhaseSelectedChoice(false),
                calendarSocialStatsWorkspacePage.NextDayText,
                calendarSocialStatsWorkspacePage.GetCalendarPhaseSelectedChoice(true));
        }

        if (!viewModel.HasSave)
        {
            return CreateGroup4EditInputs(null, null, null, null, null, string.Empty, null, string.Empty, null);
        }

        return CreateGroup4EditInputs(
            GetCurrentSocialStatRankChoice(0),
            GetCurrentSocialStatRankChoice(1),
            GetCurrentSocialStatRankChoice(4),
            GetCurrentSocialStatRankChoice(3),
            GetCurrentSocialStatRankChoice(2),
            viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture),
            GetCurrentCalendarPhaseChoice(viewModel.Calendar.DayPhaseId),
            viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture),
            GetCurrentCalendarPhaseChoice(viewModel.Calendar.NextDayPhaseId));
    }

    private SocialStatRankChoiceViewState? GetCurrentSocialStatRankChoice(int statIndex)
    {
        if (!viewModel.HasSave || (uint)statIndex >= (uint)viewModel.SocialStats.Count)
        {
            return null;
        }

        _ = SaveEditorViewModel.GetSocialStatChoices(
            statIndex,
            viewModel.SocialStats[statIndex].Points,
            out SocialStatRankChoiceViewState selectedChoice);
        return selectedChoice;
    }

    private CalendarPhaseChoiceViewState? GetCurrentCalendarPhaseChoice(int phaseId)
    {
        if (!viewModel.HasSave)
        {
            return null;
        }

        _ = SaveEditorViewModel.GetCalendarPhaseChoices(phaseId, out CalendarPhaseChoiceViewState selectedChoice);
        return selectedChoice;
    }

    private void UpdateShellState()
    {
        bool startupRefreshPending = refreshEditableFieldsAfterStartupOpen;
        bool hasSave = viewModel.HasSave;
        bool canEdit = hasSave && !isBusy && !startupRefreshPending;
        bool hasPendingEditorDrafts = HasPendingEditorDrafts();
        bool canApply = canEdit && hasPendingEditorDrafts;
        bool canSave = canEdit && viewModel.IsDirty && viewModel.CanWrite && !hasPendingEditorDrafts;
        bool canSaveAs = canSave;
        Visibility editorVisibility = hasSave ? Visibility.Visible : Visibility.Collapsed;
        bool canNavigateEditorSections = canEdit;
        bool canNavigateOverview = !isBusy && !startupRefreshPending;

        FileOpenMenuItem.IsEnabled = !isBusy && !startupRefreshPending;
        OpenButton.IsEnabled = !isBusy && !startupRefreshPending;
        ApplyButton.IsEnabled = canApply;
        SaveButton.IsEnabled = canSave;
        SaveAsButton.IsEnabled = canSaveAs;
        FileSaveMenuItem.IsEnabled = canSave;
        FileSaveAsMenuItem.IsEnabled = canSaveAs;
        SaveEditorScrollViewer.Visibility = editorVisibility;
        JumpOverviewButton.IsEnabled = canNavigateOverview;
        JumpBasicStatsButton.IsEnabled = canNavigateEditorSections;
        JumpCalendarSocialStatsButton.IsEnabled = canNavigateEditorSections;
        JumpSocialLinksButton.IsEnabled = canNavigateEditorSections;
        JumpPartyPersonaButton.IsEnabled = canNavigateEditorSections;
        JumpEquipmentButton.IsEnabled = canNavigateEditorSections;
        JumpCompendiumButton.IsEnabled = canNavigateEditorSections;
        JumpInventoryButton.IsEnabled = canNavigateEditorSections;
        JumpDiagnosticsStateButton.IsEnabled = canNavigateEditorSections;
        basicStatsWorkspacePage?.SetBasicStatsEnabled(canEdit);
        calendarSocialStatsWorkspacePage?.SetCalendarSocialStatsEnabled(canEdit);
        SocialLinkListView.IsEnabled = canEdit;
        SocialLinkAddComboBox.IsEnabled = canEdit;
        SocialLinkLevelTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        SocialLinkProgressTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        SocialLinkDeleteButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;
        CompendiumListView.IsEnabled = canEdit;
        CompendiumAddComboBox.IsEnabled = canEdit;
        CompendiumRemoveButton.IsEnabled = canEdit && selectedCompendiumListSlotIndex.HasValue;
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
        StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, hasPendingEditorDrafts, viewModel.IsDirty, viewModel.CanWrite);
        if (WorkspaceFrame.Content is OverviewWorkspacePage overviewPage)
        {
            overviewPage.SetOverviewState(CreateOverviewWorkspaceState());
        }

        UpdateShellStatusInfoBar(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateWindowTitle();
    }

    private bool HasPendingEditorDrafts() =>
        viewModel.HasSave &&
        (HasBasicStatsDraft() ||
            HasEquipmentDraft() ||
            HasGroup4Draft() ||
            HasSocialLinkAddDraft() ||
            HasSelectedSocialLinkDraft() ||
            HasCompendiumAddDraft() ||
            HasPersonaDraft() ||
            inventoryQuantityDraftDirty);

    private bool HasBasicStatsDraft() =>
        basicStatsWorkspacePage is not null &&
        basicStatsWorkspacePageInitializedFromViewModel &&
        (!string.Equals(GetCurrentFamilyNameText(), viewModel.FamilyName, StringComparison.Ordinal) ||
        !string.Equals(GetCurrentGivenNameText(), viewModel.GivenName, StringComparison.Ordinal) ||
        !string.Equals(GetCurrentYenText(), viewModel.Yen.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
        (byte)GetCurrentMainCharacterLevelRawValue() != viewModel.MainCharacterLevel ||
        !string.Equals(
            GetCurrentMainCharacterTotalExperienceText(),
            viewModel.MainCharacterTotalExperience.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal));

    private bool HasEquipmentDraft()
    {
        EquipmentCharacterViewState? selectedCharacter = GetSelectedEquipmentCharacterViewState();
        return selectedCharacter is not null &&
            (ReadEquipmentItemId(EquipmentWeaponComboBox) != selectedCharacter.WeaponItemId ||
                ReadEquipmentItemId(EquipmentArmorComboBox) != selectedCharacter.ArmorItemId ||
                ReadEquipmentItemId(EquipmentAccessoryComboBox) != selectedCharacter.AccessoryItemId ||
                ReadEquipmentItemId(EquipmentCostumeComboBox) != selectedCharacter.CostumeItemId);
    }

    private EquipmentCharacterViewState? GetSelectedEquipmentCharacterViewState() =>
        selectedEquipmentCharacterId.HasValue
            ? viewModel.EquipmentCharacters.FirstOrDefault(character => character.CharacterId == selectedEquipmentCharacterId.Value)
            : null;

    private static ushort? ReadEquipmentItemId(ComboBox comboBox) =>
        comboBox.SelectedItem is InventoryItemChoiceViewState selectedItem ? selectedItem.ItemId : null;

    private bool HasGroup4Draft()
    {
        if (!viewModel.HasSave ||
            viewModel.SocialStats.Count < 5 ||
            calendarSocialStatsWorkspacePage is null ||
            !calendarSocialStatsWorkspacePageInitializedFromViewModel)
        {
            return false;
        }

        return HasSocialStatDraft(0) ||
            HasSocialStatDraft(1) ||
            HasSocialStatDraft(4) ||
            HasSocialStatDraft(3) ||
            HasSocialStatDraft(2) ||
            !string.Equals(calendarSocialStatsWorkspacePage.DayText, viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            HasCalendarPhaseDraft(false, viewModel.Calendar.DayPhaseId) ||
            !string.Equals(calendarSocialStatsWorkspacePage.NextDayText, viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            HasCalendarPhaseDraft(true, viewModel.Calendar.NextDayPhaseId);
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

    private bool HasSocialLinkAddDraft() =>
        SocialLinkAddComboBox.SelectedItem is SocialLinkChoiceViewState selectedChoice &&
        !selectedChoice.IsPlaceholder;

    private bool HasCompendiumAddDraft() =>
        CompendiumAddComboBox.SelectedItem is PersonaChoiceViewState selectedChoice &&
        selectedChoice.PersonaId != 0;

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

    private bool HasSocialStatDraft(int statIndex) =>
        calendarSocialStatsWorkspacePage?.GetSocialStatSelectedRank(statIndex) is SocialStatRankChoiceViewState selectedRank &&
        !ShouldSkipSocialStatEdit(viewModel.SocialStats[statIndex], selectedRank);

    private bool HasCalendarPhaseDraft(bool isNextPhase, int currentPhaseId) =>
        calendarSocialStatsWorkspacePage?.GetCalendarPhaseSelectedChoice(isNextPhase) is CalendarPhaseChoiceViewState selectedPhase &&
        !ShouldSkipCalendarPhaseEdit(currentPhaseId, selectedPhase);

    private void RefreshSocialStatsState()
    {
        TraceStartup("RefreshSocialStatsState enter");
        if (calendarSocialStatsWorkspacePage is null)
        {
            calendarSocialStatsWorkspacePageInitializedFromViewModel = false;
            return;
        }

        if (!viewModel.HasSave || viewModel.SocialStats.Count < 5)
        {
            ClearSocialStatChoices(0);
            ClearSocialStatChoices(1);
            ClearSocialStatChoices(4);
            ClearSocialStatChoices(3);
            ClearSocialStatChoices(2);
            return;
        }

        SetSocialStatSelection(0);
        SetSocialStatSelection(1);
        SetSocialStatSelection(4);
        SetSocialStatSelection(3);
        SetSocialStatSelection(2);
        TraceStartup("RefreshSocialStatsState exit");
    }

    private void SetSocialStatSelection(int statIndex)
    {
        TraceStartup($"SetSocialStatSelection enter {statIndex}");
        suppressImmediateEditEvents = true;
        try
        {
            SocialStatViewState stat = viewModel.SocialStats[statIndex];
            IReadOnlyList<SocialStatRankChoiceViewState> choices = SaveEditorViewModel.GetSocialStatChoices(statIndex, stat.Points, out SocialStatRankChoiceViewState selectedChoice);
            calendarSocialStatsWorkspacePage?.SetSocialStatSelection(statIndex, choices, selectedChoice);
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
        TraceStartup($"SetSocialStatSelection exit {statIndex}");
    }

    private void ClearSocialStatChoices(int statIndex) =>
        calendarSocialStatsWorkspacePage?.ClearSocialStatChoices(statIndex);

    private void RefreshCalendarState()
    {
        TraceStartup("RefreshCalendarState enter");
        if (calendarSocialStatsWorkspacePage is null)
        {
            calendarSocialStatsWorkspacePageInitializedFromViewModel = false;
            return;
        }

        suppressImmediateEditEvents = true;
        try
        {
            if (!viewModel.HasSave)
            {
                calendarSocialStatsWorkspacePage.ClearCalendarPhaseChoices(false);
                calendarSocialStatsWorkspacePage.ClearCalendarPhaseChoices(true);
                calendarSocialStatsWorkspacePage.DayText = string.Empty;
                calendarSocialStatsWorkspacePage.NextDayText = string.Empty;
                calendarSocialStatsWorkspacePageInitializedFromViewModel = true;
                return;
            }

            calendarSocialStatsWorkspacePage.DayText = viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture);
            calendarSocialStatsWorkspacePage.NextDayText = viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture);
            IReadOnlyList<CalendarPhaseChoiceViewState> phaseChoices = SaveEditorViewModel.GetCalendarPhaseChoices(viewModel.Calendar.DayPhaseId, out CalendarPhaseChoiceViewState selectedPhase);
            calendarSocialStatsWorkspacePage.SetCalendarPhaseSelection(false, phaseChoices, selectedPhase);
            IReadOnlyList<CalendarPhaseChoiceViewState> nextPhaseChoices = SaveEditorViewModel.GetCalendarPhaseChoices(viewModel.Calendar.NextDayPhaseId, out CalendarPhaseChoiceViewState selectedNextPhase);
            calendarSocialStatsWorkspacePage.SetCalendarPhaseSelection(true, nextPhaseChoices, selectedNextPhase);
            calendarSocialStatsWorkspacePageInitializedFromViewModel = true;
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
                selectedCompendiumListSlotIndex = null;
                selectedCompendiumSlotIndex = null;
                CompendiumListView.SelectedItem = null;
                autoSelectCompendiumEntryAfterOpen = false;
                return;
            }

            CompendiumPersonaViewState? selectedEntry = ResolveSelectedCompendiumViewState(
                compendiumItems.ToArray(),
                selectedCompendiumListSlotIndex,
                autoSelectCompendiumEntryAfterOpen);

            if (selectedEntry is not null)
            {
                selectedCompendiumListSlotIndex = selectedEntry.SlotIndex;
                CompendiumListView.SelectedItem = selectedEntry;
            }
            else
            {
                selectedCompendiumListSlotIndex = null;
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

        if (!TryGuardSelectedSocialLinkDraftBeforeOperation())
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

        if (!TryGuardSelectedSocialLinkDraftBeforeOperation())
        {
            ResetSocialLinkAddChoice();
            UpdateShellState();
            return;
        }

        TrackEditorDraft();
    }

    private bool TryGuardSelectedSocialLinkDraftBeforeOperation()
    {
        if (!viewModel.HasSave || !selectedSocialLinkIndex.HasValue || !HasSelectedSocialLinkDraft())
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

        SetUiDiagnostics([CreateUiDiagnostic(
            "P4GWINUI034",
            "Apply the selected social link draft before changing social link selection.",
            "SocialLinks")]);
        return false;
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

    private async void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e)
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

        if (!TryGuardSelectedSocialLinkDraftBeforeOperation())
        {
            UpdateShellState();
            return;
        }

        int deletedSlotIndex = selectedSocialLinkIndex.Value;
        string deletedLinkDescription = SocialLinkListView.SelectedItem?.ToString() ?? "the selected social link";
        if (!await ShowConfirmationAsync(
            "Delete social link?",
            $"Delete {deletedLinkDescription}? This stages the deletion until you save.",
            "Delete"))
        {
            return;
        }

        uiDiagnosticsOverride = null;
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

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (CompendiumListView.SelectedItem is not CompendiumPersonaViewState selectedEntry)
        {
            selectedCompendiumListSlotIndex = null;
            selectedCompendiumSlotIndex = null;
            RefreshPersonaState();
            UpdateShellState();
            return;
        }

        selectedCompendiumListSlotIndex = selectedEntry.SlotIndex;
        selectedCompendiumSlotIndex = selectedEntry.SlotIndex;
        RefreshPersonaState();
        UpdateShellState();
    }

    private void CompendiumListView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (suppressCompendiumEvents ||
            selectedCompendiumSlotIndex == selectedCompendiumListSlotIndex ||
            CompendiumListView.SelectedItem is not CompendiumPersonaViewState selectedEntry ||
            !IsSelectedCompendiumListItemTapped(e, selectedEntry))
        {
            return;
        }

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        selectedCompendiumListSlotIndex = selectedEntry.SlotIndex;
        selectedCompendiumSlotIndex = selectedEntry.SlotIndex;
        RefreshPersonaState();
        UpdateShellState();
    }

    private static bool IsSelectedCompendiumListItemTapped(TappedRoutedEventArgs e, CompendiumPersonaViewState selectedEntry)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return false;
        }

        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ListViewItem item)
            {
                return ReferenceEquals(item.DataContext, selectedEntry);
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (selectedChoice.PersonaId == 0)
        {
            autoSelectCompendiumEntryAfterOpen = false;
            selectedCompendiumListSlotIndex = null;
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

        if (!TryResolveCompendiumPersonaAddTarget(
                viewModel.CompendiumPersonaSlots,
                selectedChoice.PersonaId,
                out int slotIndex,
                out bool existingSlot,
                out SaveDiagnostic? diagnostic))
        {
            SetUiDiagnostics([diagnostic!]);
            UpdateShellState();
            return;
        }

        if (existingSlot)
        {
            selectedCompendiumListSlotIndex = slotIndex;
            selectedCompendiumSlotIndex = slotIndex;
            ResetCompendiumAddChoice();
            RefreshPersonaState();
            UpdateShellState();
            return;
        }

        TrackEditorDraft();
    }

    private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI025", "Open a save before editing the compendium.", "Compendium")]);
            return;
        }

        if (!selectedCompendiumListSlotIndex.HasValue)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI026", "Select a compendium entry before removing it.", "Compendium.Item")]);
            return;
        }

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        int removedSlotIndex = selectedCompendiumListSlotIndex.Value;
        string removedEntryDescription = CompendiumListView.SelectedItem?.ToString() ?? "the selected compendium entry";
        if (!await ShowConfirmationAsync(
            "Remove compendium entry?",
            $"Remove {removedEntryDescription}? This stages the removal until you save.",
            "Remove"))
        {
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = RefreshCompendiumDraftPreservingSelection(
            () => saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.ClearCompendiumPersonaSlot(removedSlotIndex)),
            preserveSelectedCompendiumDraft => RefreshFromViewModelPreservingInventoryQuantityDraft(
                preserveSelectedCompendiumDraft: preserveSelectedCompendiumDraft),
            () =>
            {
                selectedCompendiumListSlotIndex = null;
                selectedCompendiumSlotIndex = null;
            });
        if (!result.Succeeded)
        {
            SetUiDiagnostics(result.Diagnostics);
            _ = ShowMessageAsync("Compendium remove failed", FormatDiagnostics(result.Diagnostics));
        }
    }

    private void ResetCompendiumAddChoice()
    {
        suppressCompendiumEvents = true;
        try
        {
            CompendiumAddComboBox.SelectedItem = compendiumAddChoices.FirstOrDefault(static choice => choice.PersonaId == 0);
        }
        finally
        {
            suppressCompendiumEvents = false;
        }
    }

    private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI025", "Open a save before editing the compendium.", "Compendium")]);
            return;
        }

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return;
        }

        if (!await ShowConfirmationAsync(
            "Clear compendium?",
            $"Clear all {compendiumItems.Count.ToString(CultureInfo.InvariantCulture)} visible compendium entries? This stages the clear until you save.",
            "Clear all"))
        {
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = RefreshCompendiumDraftPreservingSelection(
            () => saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(
                () => viewModel.ClearCompendiumPersonaSlots()),
            preserveSelectedCompendiumDraft => RefreshFromViewModelPreservingInventoryQuantityDraft(
                preserveSelectedCompendiumDraft: preserveSelectedCompendiumDraft),
            () =>
            {
                selectedCompendiumListSlotIndex = null;
                selectedCompendiumSlotIndex = null;
            });
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

    private bool TryGuardSelectedInventoryQuantityDraftBeforeOperation()
    {
        if (!viewModel.HasSave || !inventoryQuantityDraftDirty || !selectedInventoryItemId.HasValue)
        {
            return true;
        }

        if (!TryReadInventoryQuantity(out byte quantity))
        {
            return false;
        }

        SetUiDiagnostics([CreateUiDiagnostic(
            "P4GWINUI035",
            "Apply the selected inventory quantity draft before changing inventory selection.",
            "Inventory.Quantity")]);
        return false;
    }

    private void RestoreInventorySelectionAfterBlockedDraft() =>
        RefreshFromViewModelPreservingInventoryQuantityDraft();

    private bool TrySelectExistingInventoryEntry()
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

        selectedInventoryEntryId = null;
        return true;
    }

    private bool TryGuardSelectedPersonaDraftBeforeOperation()
    {
        if (!viewModel.HasSave ||
            (!selectedCompendiumSlotIndex.HasValue && !selectedPersonaMemberId.HasValue) ||
            !HasPersonaDraft())
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

        SetUiDiagnostics([CreateUiDiagnostic(
            "P4GWINUI036",
            "Apply the selected persona draft before changing persona selection.",
            "Persona")]);
        return false;
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
                CompendiumListView.SelectedItem = selectedCompendiumListSlotIndex.HasValue
                    ? compendiumItems.FirstOrDefault(entry => entry.SlotIndex == selectedCompendiumListSlotIndex.Value)
                    : null;
                PersonaMemberComboBox.SelectedItem = selectedPersonaMemberId.HasValue
                    ? personaMemberChoices.FirstOrDefault(member => member.MemberId == selectedPersonaMemberId.Value)
                    : null;
                PersonaSlotComboBox.SelectedItem = selectedPersonaMemberId == 0
                    ? personaSlotChoices.FirstOrDefault(slot => slot.SlotIndex == selectedPersonaSlotIndex)
                    : null;
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

        if (!TryGuardSelectedInventoryQuantityDraftBeforeOperation())
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

        if (!TryGuardSelectedInventoryQuantityDraftBeforeOperation())
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
            _ = TrySelectExistingInventoryEntry();
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

        if (!TryGuardSelectedInventoryQuantityDraftBeforeOperation())
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
            _ = TrySelectExistingInventoryEntry();

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

    private async void InventoryDeleteButton_Click(object sender, RoutedEventArgs e)
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

        ushort deletedEntryId = selectedInventoryEntryId.Value;
        string deletedEntryDescription = InventoryListView.SelectedItem?.ToString() ?? "the selected inventory entry";
        if (!await ShowConfirmationAsync(
            "Delete inventory entry?",
            $"Delete {deletedEntryDescription}? This stages the deletion until you save.",
            "Delete"))
        {
            return;
        }

        uiDiagnosticsOverride = null;
        preserveEditorTextDuringInventoryRefresh = true;
        SaveEditorOperationResult result;
        try
        {
            result = viewModel.RemoveInventoryItem(deletedEntryId);
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

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
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

        if (selectedCompendiumSlotIndex.HasValue || selectedCompendiumListSlotIndex.HasValue)
        {
            ClearSelectedCompendiumContext(ref selectedCompendiumSlotIndex);
            selectedCompendiumListSlotIndex = null;
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

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
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

        if (!TryGuardSelectedEquipmentDraftBeforeOperation())
        {
            RestoreEquipmentSelectionAfterBlockedDraft();
            UpdateShellState();
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
        TrackEquipmentDraftSelection(EquipmentWeaponComboBox);

    private void EquipmentArmorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackEquipmentDraftSelection(EquipmentArmorComboBox);

    private void EquipmentAccessoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackEquipmentDraftSelection(EquipmentAccessoryComboBox);

    private void EquipmentCostumeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackEquipmentDraftSelection(EquipmentCostumeComboBox);

    private bool TryGuardSelectedEquipmentDraftBeforeOperation()
    {
        if (!HasEquipmentDraft())
        {
            return true;
        }

        SetUiDiagnostics([CreateUiDiagnostic(
            "P4GWINUI039",
            "Apply the selected equipment draft before changing equipment character.",
            "Equipment")]);
        return false;
    }

    private void RestoreEquipmentSelectionAfterBlockedDraft()
    {
        suppressEquipmentEvents = true;
        try
        {
            EquipmentCharacterComboBox.SelectedItem = GetSelectedEquipmentCharacterViewState();
        }
        finally
        {
            suppressEquipmentEvents = false;
        }
    }

    private void TrackEquipmentDraftSelection(ComboBox comboBox)
    {
        if (suppressEquipmentEvents ||
            !selectedEquipmentCharacterId.HasValue ||
            comboBox.SelectedItem is not InventoryItemChoiceViewState)
        {
            return;
        }

        TrackEditorDraft();
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
        if (!TryReadInventoryQuantity(out _))
        {
            UpdateShellState();
            return;
        }

        uiDiagnosticsOverride = null;
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
    }

    private void FamilyNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        TrackEditorDraft();

    private void GivenNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        TrackEditorDraft();

    private void YenTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        if (!uint.TryParse(GetCurrentYenText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint yen))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI006", "Yen must be an unsigned whole number.", "Yen")]);
            UpdateShellState();
            return;
        }

        TrackEditorDraft();
    }

    private void MainCharacterTotalExperienceTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        if (!uint.TryParse(GetCurrentMainCharacterTotalExperienceText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint totalExperience))
        {
            SetUiDiagnostics([CreateUiDiagnostic(
                "P4GWINUI028",
                "Main character total experience must be an unsigned whole number.",
                "MainCharacter.TotalExperience")]);
            UpdateShellState();
            return;
        }

        TrackEditorDraft();
    }

    private void CalendarSocialStatsWorkspacePage_SocialStatSelectionChanged(
        object? sender,
        SocialStatSelectionChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        TrackSocialStatDraftEdit(calendarSocialStatsWorkspacePage?.GetSocialStatSelectedRank(e.StatIndex), e.StatIndex);
    }

    private void TrackSocialStatDraftEdit(SocialStatRankChoiceViewState? selectedRank, int statIndex)
    {
        if (selectedRank is null)
        {
            return;
        }

        if (ShouldSkipSocialStatEdit(viewModel.SocialStats[statIndex], selectedRank))
        {
            UpdateShellState();
            return;
        }

        TrackEditorDraft();
    }

    private void CalendarSocialStatsWorkspacePage_DayTextChanged(
        object? sender,
        CalendarDayTextChangedEventArgs e) =>
        TrackDayDraftEdit(e.Text, e.IsNextDay);

    private void TrackDayDraftEdit(string text, bool isNextDay)
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

        TrackEditorDraft();
    }

    private void CalendarSocialStatsWorkspacePage_PhaseSelectionChanged(
        object? sender,
        CalendarPhaseSelectionChangedEventArgs e)
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        TrackPhaseDraftEdit(
            calendarSocialStatsWorkspacePage?.GetCalendarPhaseSelectedChoice(e.IsNextPhase),
            e.IsNextPhase ? viewModel.Calendar.NextDayPhaseId : viewModel.Calendar.DayPhaseId,
            e.IsNextPhase);
    }

    private void TrackPhaseDraftEdit(
        CalendarPhaseChoiceViewState? selectedPhase,
        int currentPhaseId,
        bool isNextPhase)
    {
        if (selectedPhase is null)
        {
            return;
        }

        if (ShouldSkipCalendarPhaseEdit(currentPhaseId, selectedPhase))
        {
            UpdateShellState();
            return;
        }

        TrackEditorDraft();
    }

    private void PartySlot0ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackPartyMemberDraftEdit(PartySlot0ComboBox, 0);

    private void PartySlot1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackPartyMemberDraftEdit(PartySlot1ComboBox, 1);

    private void PartySlot2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackPartyMemberDraftEdit(PartySlot2ComboBox, 2);

    private void TrackPartyMemberDraftEdit(ComboBox comboBox, int slotIndex)
    {
        if (!CanProcessImmediateEditEvent() ||
            comboBox.SelectedItem is not PartyConfigurationChoiceViewState selectedMember)
        {
            return;
        }

        TrackEditorDraft();
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

        TrackEditorDraft();
    }

    private bool CanProcessImmediateEditEvent() =>
        !suppressImmediateEditEvents &&
        viewModel is not null &&
        viewModel.HasSave &&
        !isBusy &&
        !refreshEditableFieldsAfterStartupOpen;

    private void TrackEditorDraft()
    {
        if (!CanProcessImmediateEditEvent())
        {
            return;
        }

        uiDiagnosticsOverride = null;
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
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

    private void UpdateShellStatusInfoBar(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        if (viewModel is null)
        {
            return;
        }

        bool startupRefreshPending = refreshEditableFieldsAfterStartupOpen;
        bool isOperationBusy = isBusy || startupRefreshPending;
        ShellBusyProgressBar.IsIndeterminate = isOperationBusy;
        ShellBusyProgressBar.Visibility = isOperationBusy ? Visibility.Visible : Visibility.Collapsed;
        ShellStatusInfoBar.IsOpen = true;

        if (isOperationBusy)
        {
            ShellStatusInfoBar.Severity = InfoBarSeverity.Informational;
            ShellStatusInfoBar.Title = "Busy";
            ShellStatusInfoBar.Message = startupRefreshPending
                ? "Loading editor fields for the opened save."
                : "Working on the save file.";
            return;
        }

        SaveDiagnostic? primaryDiagnostic = GetPrimaryShellDiagnostic(diagnostics);
        if (primaryDiagnostic is not null)
        {
            ShellStatusInfoBar.Severity = ToInfoBarSeverity(primaryDiagnostic.Severity);
            ShellStatusInfoBar.Title = $"{primaryDiagnostic.Severity} {primaryDiagnostic.Code}";
            ShellStatusInfoBar.Message = FormatShellDiagnosticMessage(primaryDiagnostic);
            return;
        }

        bool hasSave = viewModel.HasSave;
        bool hasPendingEditorDrafts = HasPendingEditorDrafts();
        if (!hasSave)
        {
            ShellStatusInfoBar.Severity = InfoBarSeverity.Informational;
            ShellStatusInfoBar.Title = "No save open";
            ShellStatusInfoBar.Message = "Open or drop a Persona 4 Golden .bin save to begin editing.";
            return;
        }

        if (hasPendingEditorDrafts)
        {
            ShellStatusInfoBar.Severity = InfoBarSeverity.Warning;
            ShellStatusInfoBar.Title = "Dirty draft";
            ShellStatusInfoBar.Message = "Apply edits before saving so the pending field changes are staged.";
            return;
        }

        if (viewModel.IsDirty && viewModel.CanWrite)
        {
            ShellStatusInfoBar.Severity = InfoBarSeverity.Success;
            ShellStatusInfoBar.Title = "Ready to save";
            ShellStatusInfoBar.Message = "Applied changes are staged and can be written to disk.";
            return;
        }

        if (viewModel.IsDirty)
        {
            ShellStatusInfoBar.Severity = InfoBarSeverity.Informational;
            ShellStatusInfoBar.Title = "Write pending";
            ShellStatusInfoBar.Message = "Applied changes are waiting for save acknowledgement.";
            return;
        }

        ShellStatusInfoBar.Severity = InfoBarSeverity.Informational;
        ShellStatusInfoBar.Title = "Loaded clean";
        ShellStatusInfoBar.Message = "No unapplied changes.";
    }

    private static SaveDiagnostic? GetPrimaryShellDiagnostic(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        diagnostics.OrderByDescending(static diagnostic => GetDiagnosticPriority(diagnostic.Severity)).FirstOrDefault();

    private static int GetDiagnosticPriority(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => 3,
            DiagnosticSeverity.Warning => 2,
            DiagnosticSeverity.Info => 1,
            _ => 0,
        };

    private static InfoBarSeverity ToInfoBarSeverity(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => InfoBarSeverity.Error,
            DiagnosticSeverity.Warning => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Informational,
        };

    private static string FormatShellDiagnosticMessage(SaveDiagnostic diagnostic) =>
        string.IsNullOrWhiteSpace(diagnostic.Target)
            ? diagnostic.Message
            : $"{diagnostic.Target}: {diagnostic.Message}";

    private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        IReadOnlyList<DiagnosticListItemViewState> diagnosticItems = DiagnosticListItemViewState.FromDiagnostics(diagnostics);
        void UpdateDiagnostics()
        {
            diagnosticsItems.Clear();
            foreach (DiagnosticListItemViewState diagnosticItem in diagnosticItems)
            {
                diagnosticsItems.Add(diagnosticItem);
            }

            UpdateShellStatusInfoBar(diagnostics);
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

    private Task<bool> ConfirmOverwriteSaveAsync(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return Task.FromResult(true);
        }

        return ShowConfirmationAsync(
            "Overwrite save?",
            $"Replace {targetPath} with the edited save? This cannot be undone.",
            "Overwrite");
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message, string primaryButtonText)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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

}

internal sealed record DiagnosticListItemViewState(
    string Severity,
    string Code,
    string Target,
    string Message,
    string AutomationName)
{
    public override string ToString() =>
        AutomationName;

    internal static IReadOnlyList<DiagnosticListItemViewState> FromDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? [new("Info", "Status", "Diagnostics", "No diagnostics.", "No diagnostics.")]
            : diagnostics.Select(FromDiagnostic).ToArray();

    private static DiagnosticListItemViewState FromDiagnostic(SaveDiagnostic diagnostic)
    {
        string target = string.IsNullOrWhiteSpace(diagnostic.Target) ? "General" : diagnostic.Target;
        string automationName = string.IsNullOrWhiteSpace(diagnostic.Target)
            ? string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}")
            : string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code} [{diagnostic.Target}]: {diagnostic.Message}");

        return new(
            diagnostic.Severity.ToString(),
            diagnostic.Code,
            target,
            diagnostic.Message,
            automationName);
    }
}
