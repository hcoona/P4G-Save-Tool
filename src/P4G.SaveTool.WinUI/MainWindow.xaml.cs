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
    private bool suppressNavigationSelectionChanged;
    private bool isWorkspaceRoutingInitialized;
    private bool basicStatsWorkspacePageInitializedFromViewModel;
    private bool calendarSocialStatsWorkspacePageInitializedFromViewModel;
    private bool socialLinksWorkspacePageInitializedFromViewModel;
    private bool equipmentWorkspacePageInitializedFromViewModel;
    private bool inventoryWorkspacePageInitializedFromViewModel;
    private bool partyPersonaWorkspacePageInitializedFromViewModel;
    private bool compendiumWorkspacePageInitializedFromViewModel;
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
    private readonly PersonaEditorControl personaEditorControl;
    private BasicStatsWorkspacePage? basicStatsWorkspacePage;
    private CalendarSocialStatsWorkspacePage? calendarSocialStatsWorkspacePage;
    private SocialLinksWorkspacePage? socialLinksWorkspacePage;
    private EquipmentWorkspacePage? equipmentWorkspacePage;
    private InventoryWorkspacePage? inventoryWorkspacePage;
    private PartyPersonaWorkspacePage? partyPersonaWorkspacePage;
    private CompendiumWorkspacePage? compendiumWorkspacePage;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow(string? startupOpenPath = null)
    {
        this.startupOpenPath = startupOpenPath;
        InitializeComponent();
        ResizeToDefaultMultiPaneSize();
        personaEditorControl = new PersonaEditorControl();
        personaEditorControl.SetItemsSources(
            personaMemberChoices,
            personaSlotChoices,
            personaChoices,
            personaSkillChoices1,
            personaSkillChoices2,
            personaSkillChoices3,
            personaSkillChoices4,
            personaSkillChoices5,
            personaSkillChoices6,
            personaSkillChoices7,
            personaSkillChoices8);
        WirePersonaEditorControlEvents();

        viewModel = new SaveEditorViewModel(new SaveApplicationService());
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        SectionNavigationView.SelectedItem = JumpOverviewButton;
    }

    private void WirePersonaEditorControlEvents()
    {
        personaEditorControl.PersonaMemberSelectionChanged += PersonaMemberComboBox_SelectionChanged;
        personaEditorControl.PersonaSlotSelectionChanged += PersonaSlotComboBox_SelectionChanged;
        personaEditorControl.PersonaChoiceSelectionChanged += PersonaChoiceComboBox_SelectionChanged;
        personaEditorControl.PersonaSkillSelectionChanged += PersonaSkillBox_SelectionChanged;
        personaEditorControl.PersonaDraftTextChanged += PersonaDraftControl_Changed;
        personaEditorControl.PersonaDraftValueChanged += PersonaDraftControl_Changed;
        personaEditorControl.PersonaLevelValueChanged += PersonaLevelSlider_ValueChanged;
        personaEditorControl.PersonaCalculateFromLevelClick += PersonaCalculateFromLevelButton_Click;
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
        if (suppressNavigationSelectionChanged)
        {
            return;
        }

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

        if (sectionTag == "SocialLinks")
        {
            NavigateToSocialLinksWorkspace();
            return;
        }

        if (sectionTag == "Equipment")
        {
            NavigateToEquipmentWorkspace();
            return;
        }

        if (sectionTag == "Inventory")
        {
            NavigateToInventoryWorkspace();
            return;
        }

        if (sectionTag == "PartyPersona")
        {
            NavigateToPartyPersonaWorkspace();
            return;
        }

        if (sectionTag == "Compendium")
        {
            NavigateToCompendiumWorkspace();
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(sectionTag), sectionTag, "Unknown workspace section tag.");
    }

    private void NavigateToOverviewWorkspace()
    {
        SelectWorkspaceNavigationItem(JumpOverviewButton);

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
        SelectWorkspaceNavigationItem(JumpDiagnosticsStateButton);

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
        SelectWorkspaceNavigationItem(JumpBasicStatsButton);

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
        SelectWorkspaceNavigationItem(JumpCalendarSocialStatsButton);

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

    private void NavigateToSocialLinksWorkspace()
    {
        SelectWorkspaceNavigationItem(JumpSocialLinksButton);

        if (WorkspaceFrame.Content is SocialLinksWorkspacePage page)
        {
            ConfigureSocialLinksWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(SocialLinksWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the social links workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is SocialLinksWorkspacePage navigatedPage)
        {
            ConfigureSocialLinksWorkspacePage(navigatedPage);
        }
    }

    private void ConfigureSocialLinksWorkspacePage(SocialLinksWorkspacePage page)
    {
        socialLinksWorkspacePage = page;
        page.SetItemsSources(socialLinkItems, socialLinkChoices);
        page.SocialLinkSelectionChanged -= SocialLinkListView_SelectionChanged;
        page.SocialLinkSelectionChanged += SocialLinkListView_SelectionChanged;
        page.SocialLinkAddSelectionChanged -= SocialLinkAddComboBox_SelectionChanged;
        page.SocialLinkAddSelectionChanged += SocialLinkAddComboBox_SelectionChanged;
        page.SocialLinkTextChanged -= SocialLinkTextBox_TextChanged;
        page.SocialLinkTextChanged += SocialLinkTextBox_TextChanged;
        page.SocialLinkDeleteClick -= SocialLinkDeleteButton_Click;
        page.SocialLinkDeleteClick += SocialLinkDeleteButton_Click;

        if (!socialLinksWorkspacePageInitializedFromViewModel)
        {
            RefreshSocialLinksState();
        }

        page.SetSocialLinksEnabled(CanEditSocialLinks(), selectedSocialLinkIndex.HasValue);
    }

    private void NavigateToEquipmentWorkspace()
    {
        SelectWorkspaceNavigationItem(JumpEquipmentButton);

        if (WorkspaceFrame.Content is EquipmentWorkspacePage page)
        {
            ConfigureEquipmentWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(EquipmentWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the equipment workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is EquipmentWorkspacePage navigatedPage)
        {
            ConfigureEquipmentWorkspacePage(navigatedPage);
        }
    }

    private void ConfigureEquipmentWorkspacePage(EquipmentWorkspacePage page)
    {
        equipmentWorkspacePage = page;
        page.SetItemsSources(
            equipmentCharacters,
            equipmentWeaponChoices,
            equipmentArmorChoices,
            equipmentAccessoryChoices,
            equipmentCostumeChoices);
        page.EquipmentCharacterSelectionChanged -= EquipmentCharacterComboBox_SelectionChanged;
        page.EquipmentCharacterSelectionChanged += EquipmentCharacterComboBox_SelectionChanged;
        page.EquipmentWeaponSelectionChanged -= EquipmentWeaponComboBox_SelectionChanged;
        page.EquipmentWeaponSelectionChanged += EquipmentWeaponComboBox_SelectionChanged;
        page.EquipmentArmorSelectionChanged -= EquipmentArmorComboBox_SelectionChanged;
        page.EquipmentArmorSelectionChanged += EquipmentArmorComboBox_SelectionChanged;
        page.EquipmentAccessorySelectionChanged -= EquipmentAccessoryComboBox_SelectionChanged;
        page.EquipmentAccessorySelectionChanged += EquipmentAccessoryComboBox_SelectionChanged;
        page.EquipmentCostumeSelectionChanged -= EquipmentCostumeComboBox_SelectionChanged;
        page.EquipmentCostumeSelectionChanged += EquipmentCostumeComboBox_SelectionChanged;

        if (!equipmentWorkspacePageInitializedFromViewModel)
        {
            RefreshEquipmentState();
        }

        page.SetEquipmentEnabled(CanEditEquipment());
    }

    private void NavigateToInventoryWorkspace()
    {
        SelectWorkspaceNavigationItem(JumpInventoryButton);

        if (WorkspaceFrame.Content is InventoryWorkspacePage page)
        {
            ConfigureInventoryWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(InventoryWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the inventory workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is InventoryWorkspacePage navigatedPage)
        {
            ConfigureInventoryWorkspacePage(navigatedPage);
        }
    }

    private void ConfigureInventoryWorkspacePage(InventoryWorkspacePage page)
    {
        inventoryWorkspacePage = page;
        page.SetItemsSources(inventoryItems, inventoryCategories, inventoryItemChoices);
        page.InventorySelectionChanged -= InventoryListView_SelectionChanged;
        page.InventorySelectionChanged += InventoryListView_SelectionChanged;
        page.InventoryCategorySelectionChanged -= InventoryCategoryComboBox_SelectionChanged;
        page.InventoryCategorySelectionChanged += InventoryCategoryComboBox_SelectionChanged;
        page.InventoryItemSelectionChanged -= InventoryItemComboBox_SelectionChanged;
        page.InventoryItemSelectionChanged += InventoryItemComboBox_SelectionChanged;
        page.InventoryQuantityTextChanged -= InventoryQuantityTextBox_TextChanged;
        page.InventoryQuantityTextChanged += InventoryQuantityTextBox_TextChanged;
        page.InventoryAddUpdateClick -= InventoryAddUpdateButton_Click;
        page.InventoryAddUpdateClick += InventoryAddUpdateButton_Click;
        page.InventoryDeleteClick -= InventoryDeleteButton_Click;
        page.InventoryDeleteClick += InventoryDeleteButton_Click;

        if (!inventoryWorkspacePageInitializedFromViewModel)
        {
            RefreshInventoryState();
        }

        page.SetInventoryEnabled(CanEditInventory(), selectedInventoryItemId.HasValue, selectedInventoryEntryId.HasValue);
    }

    private void NavigateToPartyPersonaWorkspace()
    {
        if (!TryClearCompendiumEditorContextForPartyPersonaNavigation())
        {
            NavigateToCompendiumWorkspace();
            return;
        }

        SelectWorkspaceNavigationItem(JumpPartyPersonaButton);

        if (WorkspaceFrame.Content is PartyPersonaWorkspacePage page)
        {
            ConfigurePartyPersonaWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(PartyPersonaWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the party and persona workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is PartyPersonaWorkspacePage navigatedPage)
        {
            ConfigurePartyPersonaWorkspacePage(navigatedPage);
        }
    }

    private void ConfigurePartyPersonaWorkspacePage(PartyPersonaWorkspacePage page)
    {
        partyPersonaWorkspacePage = page;
        page.SetItemsSources(partySlot0Choices, partySlot1Choices, partySlot2Choices);
        compendiumWorkspacePage?.ClearPersonaEditor(personaEditorControl);
        page.SetPersonaEditor(personaEditorControl);
        page.PartySlot0SelectionChanged -= PartySlot0ComboBox_SelectionChanged;
        page.PartySlot0SelectionChanged += PartySlot0ComboBox_SelectionChanged;
        page.PartySlot1SelectionChanged -= PartySlot1ComboBox_SelectionChanged;
        page.PartySlot1SelectionChanged += PartySlot1ComboBox_SelectionChanged;
        page.PartySlot2SelectionChanged -= PartySlot2ComboBox_SelectionChanged;
        page.PartySlot2SelectionChanged += PartySlot2ComboBox_SelectionChanged;

        if (!partyPersonaWorkspacePageInitializedFromViewModel)
        {
            RefreshPartyConfigurationState();
        }

        page.SetPartyConfigurationEnabled(CanEditPartyPersona());
    }

    private bool TryClearCompendiumEditorContextForPartyPersonaNavigation()
    {
        if (!selectedCompendiumSlotIndex.HasValue)
        {
            return true;
        }

        if (!TryGuardSelectedPersonaDraftBeforeOperation())
        {
            RestorePersonaSelectionAfterBlockedDraft();
            UpdateShellState();
            return false;
        }

        ClearSelectedCompendiumContext(ref selectedCompendiumSlotIndex);
        RefreshPersonaState();
        UpdateShellState();
        return true;
    }

    private void NavigateToCompendiumWorkspace()
    {
        SelectWorkspaceNavigationItem(JumpCompendiumButton);

        if (WorkspaceFrame.Content is CompendiumWorkspacePage page)
        {
            ConfigureCompendiumWorkspacePage(page);
            return;
        }

        if (!WorkspaceFrame.Navigate(typeof(CompendiumWorkspacePage)))
        {
            throw new InvalidOperationException("Could not navigate to the compendium workspace page.");
        }

        WorkspaceFrame.BackStack.Clear();
        if (WorkspaceFrame.Content is CompendiumWorkspacePage navigatedPage)
        {
            ConfigureCompendiumWorkspacePage(navigatedPage);
        }
    }

    private void ConfigureCompendiumWorkspacePage(CompendiumWorkspacePage page)
    {
        compendiumWorkspacePage = page;
        page.SetItemsSources(compendiumItems, compendiumAddChoices);
        partyPersonaWorkspacePage?.ClearPersonaEditor(personaEditorControl);
        page.SetPersonaEditor(personaEditorControl);
        page.CompendiumSelectionChanged -= CompendiumListView_SelectionChanged;
        page.CompendiumSelectionChanged += CompendiumListView_SelectionChanged;
        page.CompendiumTapped -= CompendiumListView_Tapped;
        page.CompendiumTapped += CompendiumListView_Tapped;
        page.CompendiumAddSelectionChanged -= CompendiumAddComboBox_SelectionChanged;
        page.CompendiumAddSelectionChanged += CompendiumAddComboBox_SelectionChanged;
        page.CompendiumRemoveClick -= CompendiumRemoveButton_Click;
        page.CompendiumRemoveClick += CompendiumRemoveButton_Click;
        page.CompendiumClearClick -= CompendiumClearButton_Click;
        page.CompendiumClearClick += CompendiumClearButton_Click;

        if (!compendiumWorkspacePageInitializedFromViewModel)
        {
            RefreshCompendiumState();
        }

        RefreshPersonaSummary();
        page.SetCompendiumEnabled(CanEditCompendium(), selectedCompendiumListSlotIndex.HasValue, compendiumItems.Count > 0);
    }

    private void SelectWorkspaceNavigationItem(NavigationViewItem navigationItem)
    {
        if (SectionNavigationView.SelectedItem is NavigationViewItem selectedItem &&
            selectedItem.Tag is string selectedTag &&
            navigationItem.Tag is string targetTag &&
            string.Equals(selectedTag, targetTag, StringComparison.Ordinal))
        {
            return;
        }

        if (ReferenceEquals(SectionNavigationView.SelectedItem, navigationItem))
        {
            return;
        }

        suppressNavigationSelectionChanged = true;
        try
        {
            SectionNavigationView.SelectedItem = navigationItem;
        }
        finally
        {
            suppressNavigationSelectionChanged = false;
        }
    }

    private void WorkspaceFrame_NavigationFailed(object sender, NavigationFailedEventArgs e) =>
        throw new InvalidOperationException($"Could not navigate to workspace page {e.SourcePageType.FullName}.", e.Exception);

    private void PersonaCalculateFromLevelButton_Click(object sender, RoutedEventArgs e) =>
        personaEditorControl.ExperienceText = LevelExperienceProjection.CalculateTotalExperienceFromLevel((byte)personaEditorControl.Level).ToString(CultureInfo.InvariantCulture);

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

    private void UpdatePersonaLevelValueText()
    {
        bool hasSave = viewModel is not null && viewModel.HasSave;
        double level = personaEditorControl.Level;
        personaEditorControl.SetLevelValueText(hasSave
            ? ((byte)Math.Round(level, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
            : string.Empty);
        personaEditorControl.SetLevelValueForeground(
            hasSave && IsLegacyLevelWarningValue(level)
                ? legacyLevelWarningForeground
                : personaEditorControl.DefaultLevelValueForeground);
    }

    internal static bool IsLegacyLevelWarningValue(double level) =>
        Math.Round(level, MidpointRounding.AwayFromZero) > 99;

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
                    if (inventoryWorkspacePage is not null)
                    {
                        inventoryWorkspacePage.QuantityText = string.Empty;
                    }
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
        AddPartyMemberValue(GetCurrentPartyMemberChoice(0), 0, batch, validationDiagnostics);
        AddPartyMemberValue(GetCurrentPartyMemberChoice(1), 1, batch, validationDiagnostics);
        AddPartyMemberValue(GetCurrentPartyMemberChoice(2), 2, batch, validationDiagnostics);
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
            inventoryWorkspacePage?.QuantityText ?? string.Empty,
            batch,
            validationDiagnostics);

        return TryFinalizeEditBatch(batch, validationDiagnostics, out edits, out diagnostics);
    }

    private static void AddPartyMemberValue(
        PartyConfigurationChoiceViewState? selectedMember,
        int slotIndex,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (selectedMember is not null)
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
            equipmentWorkspacePage?.SelectedWeapon,
            selectedCharacter.WeaponItemId,
            "Equipment.Weapon",
            itemId => new SetEquippedWeaponEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
        AddEquipmentEdit(
            equipmentWorkspacePage?.SelectedArmor,
            selectedCharacter.ArmorItemId,
            "Equipment.Armor",
            itemId => new SetEquippedArmorEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
        AddEquipmentEdit(
            equipmentWorkspacePage?.SelectedAccessory,
            selectedCharacter.AccessoryItemId,
            "Equipment.Accessory",
            itemId => new SetEquippedAccessoryEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
        AddEquipmentEdit(
            equipmentWorkspacePage?.SelectedCostume,
            selectedCharacter.CostumeItemId,
            "Equipment.Costume",
            itemId => new SetEquippedCostumeEdit(selectedCharacter.CharacterId, itemId),
            edits,
            diagnostics);
    }

    private static void AddEquipmentEdit(
        InventoryItemChoiceViewState? selectedItem,
        ushort currentItemId,
        string diagnosticTarget,
        Func<ushort, SaveEditCommand> createEdit,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (selectedItem is null)
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

        if (socialLinksWorkspacePage is null)
        {
            return true;
        }

        return TryBuildSocialLinkEdits(
            selectedSocialLinkIndex,
            socialLinksWorkspacePage.SocialLinkLevelText,
            socialLinksWorkspacePage.SocialLinkProgressText,
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
        if (!selectedSocialLinkIndex.HasValue ||
            socialLinksWorkspacePage?.SelectedSocialLink is not SocialLinkViewState selectedLink)
        {
            return null;
        }

        return new SocialLinkDraftState(
            selectedLink.SlotIndex,
            selectedLink.LinkId,
            socialLinksWorkspacePage.SocialLinkLevelText,
            socialLinksWorkspacePage.SocialLinkProgressText);
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
            if (socialLinksWorkspacePage is not null)
            {
                socialLinksWorkspacePage.SelectedSocialLink = selectedLink;
            }
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

        if (socialLinksWorkspacePage is not null)
        {
            socialLinksWorkspacePage.SocialLinkLevelText = socialLinkDraft.LevelText;
            socialLinksWorkspacePage.SocialLinkProgressText = socialLinkDraft.ProgressText;
        }
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
            personaEditorControl.ExperienceText,
            personaEditorControl.Level,
            personaEditorControl.Strength,
            personaEditorControl.Magic,
            personaEditorControl.Endurance,
            personaEditorControl.Agility,
            personaEditorControl.Luck,
            ReadSkillId(personaEditorControl.SelectedSkill1),
            ReadSkillId(personaEditorControl.SelectedSkill2),
            ReadSkillId(personaEditorControl.SelectedSkill3),
            ReadSkillId(personaEditorControl.SelectedSkill4),
            ReadSkillId(personaEditorControl.SelectedSkill5),
            ReadSkillId(personaEditorControl.SelectedSkill6),
            ReadSkillId(personaEditorControl.SelectedSkill7),
            ReadSkillId(personaEditorControl.SelectedSkill8));
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

            personaEditorControl.SelectedPersona = selectedCompendiumChoice;
            personaEditorControl.ExperienceText = compendiumDraft.ExperienceText;
            personaEditorControl.Level = compendiumDraft.Level;
            personaEditorControl.Strength = compendiumDraft.Strength;
            personaEditorControl.Magic = compendiumDraft.Magic;
            personaEditorControl.Endurance = compendiumDraft.Endurance;
            personaEditorControl.Agility = compendiumDraft.Agility;
            personaEditorControl.Luck = compendiumDraft.Luck;
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

        if (socialLinksWorkspacePage?.SelectedSocialLinkAddChoice is not SocialLinkChoiceViewState selectedChoice ||
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

        if (compendiumWorkspacePage?.SelectedCompendiumAddChoice is not PersonaChoiceViewState selectedChoice ||
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
            personaEditorControl.ExperienceText,
            skillIds,
            personaEditorControl.Level,
            personaEditorControl.Strength,
            personaEditorControl.Magic,
            personaEditorControl.Endurance,
            personaEditorControl.Agility,
            personaEditorControl.Luck,
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
        if (personaEditorControl.SelectedPersona is PersonaChoiceViewState selectedPersona)
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
            ReadSkillId(personaEditorControl.SelectedSkill1),
            ReadSkillId(personaEditorControl.SelectedSkill2),
            ReadSkillId(personaEditorControl.SelectedSkill3),
            ReadSkillId(personaEditorControl.SelectedSkill4),
            ReadSkillId(personaEditorControl.SelectedSkill5),
            ReadSkillId(personaEditorControl.SelectedSkill6),
            ReadSkillId(personaEditorControl.SelectedSkill7),
            ReadSkillId(personaEditorControl.SelectedSkill8),
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

    private static ushort ReadSkillId(SkillChoiceViewState? selectedSkill) =>
        selectedSkill is not null ? selectedSkill.SkillId : ushort.MaxValue;

    private static ushort ReadPersonaId(PersonaChoiceViewState? selectedPersona) =>
        selectedPersona is not null ? selectedPersona.PersonaId : ushort.MaxValue;

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
        string inventoryQuantityDraft = inventoryWorkspacePage?.QuantityText ?? string.Empty;
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
                if (inventoryWorkspacePage is not null)
                {
                    inventoryWorkspacePage.QuantityText = inventoryQuantityDraft;
                }
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

    private bool CanEditSocialLinks() =>
        viewModel.HasSave && !isBusy && !refreshEditableFieldsAfterStartupOpen;

    private bool CanEditEquipment() =>
        viewModel.HasSave && !isBusy && !refreshEditableFieldsAfterStartupOpen;

    private bool CanEditInventory() =>
        viewModel.HasSave && !isBusy && !refreshEditableFieldsAfterStartupOpen;

    private bool CanEditPartyPersona() =>
        viewModel.HasSave && !isBusy && !refreshEditableFieldsAfterStartupOpen;

    private bool CanEditCompendium() =>
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
        bool canNavigateEditorSections = canEdit;
        bool canNavigateOverview = !isBusy && !startupRefreshPending;

        FileOpenMenuItem.IsEnabled = !isBusy && !startupRefreshPending;
        OpenButton.IsEnabled = !isBusy && !startupRefreshPending;
        ApplyButton.IsEnabled = canApply;
        SaveButton.IsEnabled = canSave;
        SaveAsButton.IsEnabled = canSaveAs;
        FileSaveMenuItem.IsEnabled = canSave;
        FileSaveAsMenuItem.IsEnabled = canSaveAs;
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
        socialLinksWorkspacePage?.SetSocialLinksEnabled(canEdit, selectedSocialLinkIndex.HasValue);
        equipmentWorkspacePage?.SetEquipmentEnabled(canEdit);
        compendiumWorkspacePage?.SetCompendiumEnabled(canEdit, selectedCompendiumListSlotIndex.HasValue, compendiumItems.Count > 0);
        partyPersonaWorkspacePage?.SetPartyConfigurationEnabled(canEdit);
        personaEditorControl.SetPersonaEditorEnabled(
            canEdit,
            selectedPersonaMemberId == 0 && !selectedCompendiumSlotIndex.HasValue,
            !selectedCompendiumSlotIndex.HasValue);
        inventoryWorkspacePage?.SetInventoryEnabled(canEdit, selectedInventoryItemId.HasValue, selectedInventoryEntryId.HasValue);

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
            HasPartyConfigurationDraft() ||
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
            equipmentWorkspacePageInitializedFromViewModel &&
            equipmentWorkspacePage is not null &&
            (ReadEquipmentItemId(equipmentWorkspacePage.SelectedWeapon) != selectedCharacter.WeaponItemId ||
                ReadEquipmentItemId(equipmentWorkspacePage.SelectedArmor) != selectedCharacter.ArmorItemId ||
                ReadEquipmentItemId(equipmentWorkspacePage.SelectedAccessory) != selectedCharacter.AccessoryItemId ||
                ReadEquipmentItemId(equipmentWorkspacePage.SelectedCostume) != selectedCharacter.CostumeItemId);
    }

    private EquipmentCharacterViewState? GetSelectedEquipmentCharacterViewState() =>
        selectedEquipmentCharacterId.HasValue
            ? viewModel.EquipmentCharacters.FirstOrDefault(character => character.CharacterId == selectedEquipmentCharacterId.Value)
            : null;

    private static ushort? ReadEquipmentItemId(InventoryItemChoiceViewState? selectedItem) =>
        selectedItem?.ItemId;

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
        if (!selectedSocialLinkIndex.HasValue || socialLinksWorkspacePage is null)
        {
            return false;
        }

        SocialLinkViewState? selectedLink = viewModel.SocialLinks.FirstOrDefault(link => link.SlotIndex == selectedSocialLinkIndex.Value);
        return selectedLink is not null &&
            (!string.Equals(socialLinksWorkspacePage.SocialLinkLevelText, selectedLink.Level.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                !string.Equals(socialLinksWorkspacePage.SocialLinkProgressText, selectedLink.Progress.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    private bool HasSocialLinkAddDraft() =>
        socialLinksWorkspacePage?.SelectedSocialLinkAddChoice is SocialLinkChoiceViewState selectedChoice &&
        !selectedChoice.IsPlaceholder;

    private bool HasCompendiumAddDraft() =>
        compendiumWorkspacePage?.SelectedCompendiumAddChoice is PersonaChoiceViewState selectedChoice &&
        selectedChoice.PersonaId != 0;

    private bool HasPartyConfigurationDraft()
    {
        if (partyPersonaWorkspacePage is null ||
            !partyPersonaWorkspacePageInitializedFromViewModel ||
            !viewModel.HasSave ||
            viewModel.PartyMembers.Count < 3)
        {
            return false;
        }

        return HasPartySlotDraft(0) || HasPartySlotDraft(1) || HasPartySlotDraft(2);
    }

    private bool HasPartySlotDraft(int slotIndex) =>
        partyPersonaWorkspacePage?.GetSelectedPartySlot(slotIndex) is PartyConfigurationChoiceViewState selectedMember &&
        selectedMember.MemberValue != viewModel.PartyMembers[slotIndex].MemberValue;

    private bool HasPersonaDraft()
    {
        PersonaSlotViewState? selectedSlot = GetSelectedPersonaSlotViewState();
        if (selectedSlot is null)
        {
            return false;
        }

        return ReadPersonaId(personaEditorControl.SelectedPersona) != selectedSlot.PersonaId ||
            !string.Equals(personaEditorControl.ExperienceText, selectedSlot.TotalExperience.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            (byte)personaEditorControl.Level != selectedSlot.Level ||
            (byte)personaEditorControl.Strength != selectedSlot.Strength ||
            (byte)personaEditorControl.Magic != selectedSlot.Magic ||
            (byte)personaEditorControl.Endurance != selectedSlot.Endurance ||
            (byte)personaEditorControl.Agility != selectedSlot.Agility ||
            (byte)personaEditorControl.Luck != selectedSlot.Luck ||
            ReadSkillId(personaEditorControl.SelectedSkill1) != selectedSlot.SkillIds[0] ||
            ReadSkillId(personaEditorControl.SelectedSkill2) != selectedSlot.SkillIds[1] ||
            ReadSkillId(personaEditorControl.SelectedSkill3) != selectedSlot.SkillIds[2] ||
            ReadSkillId(personaEditorControl.SelectedSkill4) != selectedSlot.SkillIds[3] ||
            ReadSkillId(personaEditorControl.SelectedSkill5) != selectedSlot.SkillIds[4] ||
            ReadSkillId(personaEditorControl.SelectedSkill6) != selectedSlot.SkillIds[5] ||
            ReadSkillId(personaEditorControl.SelectedSkill7) != selectedSlot.SkillIds[6] ||
            ReadSkillId(personaEditorControl.SelectedSkill8) != selectedSlot.SkillIds[7];
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

            SocialLinksWorkspacePage? page = socialLinksWorkspacePage;
            if (page is null)
            {
                socialLinksWorkspacePageInitializedFromViewModel = false;
                TraceStartup("RefreshSocialLinksState no page exit");
                return;
            }

            TraceStartup("RefreshSocialLinksState before selection-state check");
            if (!viewModel.HasSave || viewModel.SocialLinks.Count == 0)
            {
                ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);
                TraceStartup("RefreshSocialLinksState empty branch before list selection");
                page.SelectedSocialLink = null;
                page.SetEmptyStateVisible(viewModel.HasSave);
                TraceStartup("RefreshSocialLinksState empty branch after list selection");
                page.SelectedSocialLinkAddChoice = viewModel.HasSave ? blankChoice : null;
                TraceStartup("RefreshSocialLinksState empty branch after add selection");
                page.SocialLinkLevelText = string.Empty;
                page.SocialLinkProgressText = string.Empty;
                socialLinksWorkspacePageInitializedFromViewModel = true;
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
                page.SelectedSocialLink = null;
                page.SetEmptyStateVisible(viewModel.HasSave);
                page.SelectedSocialLinkAddChoice = blankChoice;
                page.SocialLinkLevelText = string.Empty;
                page.SocialLinkProgressText = string.Empty;
                socialLinksWorkspacePageInitializedFromViewModel = true;
                return;
            }
            TraceStartup("RefreshSocialLinksState resolved selection");
            selectedSocialLinkIndex = selectedLink.SlotIndex;
            selectedSocialLinkLinkId = selectedLink.LinkId;

            TraceStartup("RefreshSocialLinksState selecting list item");
            page.SelectedSocialLink = selectedLink;
            page.SetEmptyStateVisible(false);
            TraceStartup("RefreshSocialLinksState selected list item");
            page.SelectedSocialLinkAddChoice = blankChoice;
            TraceStartup("RefreshSocialLinksState selected add choice");
            page.SocialLinkLevelText = selectedLink.Level.ToString(CultureInfo.InvariantCulture);
            page.SocialLinkProgressText = selectedLink.Progress.ToString(CultureInfo.InvariantCulture);
            socialLinksWorkspacePageInitializedFromViewModel = true;
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

            CompendiumWorkspacePage? page = compendiumWorkspacePage;
            if (page is null)
            {
                compendiumWorkspacePageInitializedFromViewModel = false;
                return;
            }

            page.SelectedCompendiumAddChoice = viewModel.HasSave ? blankChoice : null;

            if (!viewModel.HasSave || compendiumItems.Count == 0)
            {
                selectedCompendiumListSlotIndex = null;
                selectedCompendiumSlotIndex = null;
                page.SelectedCompendiumEntry = null;
                autoSelectCompendiumEntryAfterOpen = false;
                compendiumWorkspacePageInitializedFromViewModel = true;
                return;
            }

            CompendiumPersonaViewState? selectedEntry = ResolveSelectedCompendiumViewState(
                compendiumItems.ToArray(),
                selectedCompendiumListSlotIndex,
                autoSelectCompendiumEntryAfterOpen);

            if (selectedEntry is not null)
            {
                selectedCompendiumListSlotIndex = selectedEntry.SlotIndex;
                page.SelectedCompendiumEntry = selectedEntry;
            }
            else
            {
                selectedCompendiumListSlotIndex = null;
                selectedCompendiumSlotIndex = null;
                page.SelectedCompendiumEntry = null;
            }
            autoSelectCompendiumEntryAfterOpen = false;
            compendiumWorkspacePageInitializedFromViewModel = true;
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

            InventoryWorkspacePage? page = inventoryWorkspacePage;
            if (page is null)
            {
                inventoryWorkspacePageInitializedFromViewModel = false;
                return;
            }

            if (!viewModel.HasSave || SaveEditorViewModel.InventoryCategories.Count == 0)
            {
                autoSelectInventoryEntryAfterOpen = false;
                selectedInventoryCategoryId = null;
                selectedInventoryItemId = null;
                selectedInventoryEntryId = null;
                page.SelectedCategory = null;
                inventoryItemChoices.Clear();
                page.SelectedItem = null;
                page.SelectedInventoryEntry = null;
                if (inventorySelectionState.ShouldHydrateQuantityText(null, null, null, string.Empty))
                {
                    page.QuantityText = string.Empty;
                }
                page.SetAddUpdateButtonText("Add/Update");
                inventoryWorkspacePageInitializedFromViewModel = true;
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
                page.SelectedCategory = null;
                inventoryItemChoices.Clear();
                page.SelectedItem = null;
                page.SelectedInventoryEntry = null;
                if (inventorySelectionState.ShouldHydrateQuantityText(null, null, null, string.Empty))
                {
                    page.QuantityText = string.Empty;
                }
                page.SetAddUpdateButtonText("Add/Update");
                inventoryWorkspacePageInitializedFromViewModel = true;
                return;
            }

            page.SelectedCategory = selectedCategory;

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
            page.SelectedItem = selectedItem;
            selectedInventoryItemId = selectedItem is { IsPlaceholder: false } ? selectedItem.ItemId : null;

            page.SelectedInventoryEntry = selectedEntry;
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
                page.QuantityText = inventoryQuantityText;
            }
            page.SetAddUpdateButtonText(selectedItem is null || selectedItem.IsPlaceholder || selectedEntry is null ? "Add/Update" : "Update");
            inventoryWorkspacePageInitializedFromViewModel = true;
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

        if (socialLinksWorkspacePage?.SelectedSocialLink is not SocialLinkViewState selectedLink)
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

        if (socialLinksWorkspacePage?.SelectedSocialLinkAddChoice is not SocialLinkChoiceViewState selectedChoice ||
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
            if (socialLinksWorkspacePage is not null)
            {
                socialLinksWorkspacePage.SelectedSocialLink = GetSelectedSocialLinkViewState();
            }
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
            if (socialLinksWorkspacePage is not null)
            {
                socialLinksWorkspacePage.SelectedSocialLinkAddChoice = socialLinkChoices.FirstOrDefault(static choice => choice.IsPlaceholder);
            }
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
        string deletedLinkDescription = socialLinksWorkspacePage?.SelectedSocialLinkDescription ?? "the selected social link";
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

        if (compendiumWorkspacePage?.SelectedCompendiumEntry is not CompendiumPersonaViewState selectedEntry)
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
            compendiumWorkspacePage?.SelectedCompendiumEntry is not CompendiumPersonaViewState selectedEntry ||
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

        if (compendiumWorkspacePage?.SelectedCompendiumAddChoice is not PersonaChoiceViewState selectedChoice)
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
                if (compendiumWorkspacePage is not null)
                {
                    compendiumWorkspacePage.SelectedCompendiumEntry = null;
                }
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
        string removedEntryDescription = compendiumWorkspacePage?.SelectedCompendiumEntryDescription ?? "the selected compendium entry";
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
            if (compendiumWorkspacePage is not null)
            {
                compendiumWorkspacePage.SelectedCompendiumAddChoice = compendiumAddChoices.FirstOrDefault(static choice => choice.PersonaId == 0);
            }
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
            PartyPersonaWorkspacePage? page = partyPersonaWorkspacePage;
            if (page is null)
            {
                partyPersonaWorkspacePageInitializedFromViewModel = false;
                return;
            }

            SetPartyConfigurationChoices(partySlot0Choices, page, 0);
            SetPartyConfigurationChoices(partySlot1Choices, page, 1);
            SetPartyConfigurationChoices(partySlot2Choices, page, 2);
            partyPersonaWorkspacePageInitializedFromViewModel = true;
        }
        finally
        {
            suppressImmediateEditEvents = false;
        }
    }

    private void SetPartyConfigurationChoices(
        ObservableCollection<PartyConfigurationChoiceViewState> targetCollection,
        PartyPersonaWorkspacePage page,
        int slotIndex)
    {
        targetCollection.Clear();
        if (!viewModel.HasSave || viewModel.PartyMembers.Count <= slotIndex)
        {
            page.SetSelectedPartySlot(slotIndex, null);
            return;
        }

        byte currentMemberValue = viewModel.PartyMembers[slotIndex].MemberValue;
        IReadOnlyList<PartyConfigurationChoiceViewState> choices =
            SaveEditorViewModel.GetPartyConfigurationChoices(currentMemberValue, out PartyConfigurationChoiceViewState selectedChoice);
        foreach (PartyConfigurationChoiceViewState choice in choices)
        {
            targetCollection.Add(choice);
        }

        page.SetSelectedPartySlot(slotIndex, selectedChoice);
    }

    private PartyConfigurationChoiceViewState? GetCurrentPartyMemberChoice(int slotIndex)
    {
        if (partyPersonaWorkspacePage is not null && partyPersonaWorkspacePageInitializedFromViewModel)
        {
            return partyPersonaWorkspacePage.GetSelectedPartySlot(slotIndex);
        }

        if (!viewModel.HasSave || viewModel.PartyMembers.Count <= slotIndex)
        {
            return null;
        }

        byte currentMemberValue = viewModel.PartyMembers[slotIndex].MemberValue;
        _ = SaveEditorViewModel.GetPartyConfigurationChoices(currentMemberValue, out PartyConfigurationChoiceViewState selectedChoice);
        return selectedChoice;
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

            EquipmentWorkspacePage? page = equipmentWorkspacePage;
            if (page is null)
            {
                equipmentWorkspacePageInitializedFromViewModel = false;
                return;
            }

            if (!viewModel.HasSave || equipmentCharacters.Count == 0)
            {
                selectedEquipmentCharacterId = null;
                page.SelectedCharacter = null;
                equipmentWeaponChoices.Clear();
                equipmentArmorChoices.Clear();
                equipmentAccessoryChoices.Clear();
                equipmentCostumeChoices.Clear();
                page.SelectedWeapon = null;
                page.SelectedArmor = null;
                page.SelectedAccessory = null;
                page.SelectedCostume = null;
                equipmentWorkspacePageInitializedFromViewModel = true;
                return;
            }

            EquipmentCharacterViewState? selectedCharacter = null;
            if (selectedEquipmentCharacterId.HasValue)
            {
                selectedCharacter = equipmentCharacters.FirstOrDefault(
                    character => character.CharacterId == selectedEquipmentCharacterId.Value);
            }

            selectedCharacter ??= equipmentCharacters[0];
            page.SelectedCharacter = selectedCharacter;
            selectedEquipmentCharacterId = selectedCharacter.CharacterId;

            page.SelectedWeapon = SetEquipmentChoices(equipmentWeaponChoices, SaveEditorViewModel.GetWeaponChoices(selectedCharacter.CharacterId), selectedCharacter.WeaponItemId);
            page.SelectedArmor = SetEquipmentChoices(equipmentArmorChoices, SaveEditorViewModel.GetArmorChoices(), selectedCharacter.ArmorItemId);
            page.SelectedAccessory = SetEquipmentChoices(equipmentAccessoryChoices, SaveEditorViewModel.GetAccessoryChoices(), selectedCharacter.AccessoryItemId);
            page.SelectedCostume = SetEquipmentChoices(equipmentCostumeChoices, SaveEditorViewModel.GetCostumeChoices(), selectedCharacter.CostumeItemId);
            equipmentWorkspacePageInitializedFromViewModel = true;
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
                personaEditorControl.SelectedMember = null;
                personaSlotChoices.Clear();
                personaEditorControl.SelectedSlot = null;
                personaChoices.Clear();
                personaEditorControl.SelectedPersona = null;
                personaEditorControl.ExperienceText = string.Empty;
                personaEditorControl.Level = 0;
                personaEditorControl.Strength = 0;
                personaEditorControl.Magic = 0;
                personaEditorControl.Endurance = 0;
                personaEditorControl.Agility = 0;
                personaEditorControl.Luck = 0;
                personaEditorControl.SelectedSkill1 = null;
                personaEditorControl.SelectedSkill2 = null;
                personaEditorControl.SelectedSkill3 = null;
                personaEditorControl.SelectedSkill4 = null;
                personaEditorControl.SelectedSkill5 = null;
                personaEditorControl.SelectedSkill6 = null;
                personaEditorControl.SelectedSkill7 = null;
                personaEditorControl.SelectedSkill8 = null;
                return;
            }

            if (selectedCompendiumSlotIndex.HasValue)
            {
                PersonaSlotViewState currentCompendiumSlot = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value];
                (selectedPersonaMemberId, selectedPersonaSlotIndex) =
                    PreserveSelectedPersonaSelectionDuringCompendiumRefresh(selectedPersonaMemberId, selectedPersonaSlotIndex);
                personaEditorControl.SelectedMember = null;
                personaSlotChoices.Clear();
                personaEditorControl.SelectedSlot = null;
                personaChoices.Clear();
                PersonaChoiceViewState selectedCompendiumChoice = default!;
                foreach (PersonaChoiceViewState choice in SaveEditorViewModel.GetPersonaChoices(
                    currentCompendiumSlot.PersonaId,
                    out selectedCompendiumChoice))
                {
                    personaChoices.Add(choice);
                }
                personaEditorControl.SelectedPersona = selectedCompendiumChoice;
                personaEditorControl.ExperienceText = currentCompendiumSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
                personaEditorControl.Level = currentCompendiumSlot.Level;
                personaEditorControl.Strength = currentCompendiumSlot.Strength;
                personaEditorControl.Magic = currentCompendiumSlot.Magic;
                personaEditorControl.Endurance = currentCompendiumSlot.Endurance;
                personaEditorControl.Agility = currentCompendiumSlot.Agility;
                personaEditorControl.Luck = currentCompendiumSlot.Luck;
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
            personaEditorControl.SelectedMember = selectedMember;
            selectedPersonaMemberId = selectedMember.MemberId;

            bool isProtagonist = selectedMember.MemberId == 0;
            IReadOnlyList<PersonaSlotViewState> personaSlots = isProtagonist
                ? viewModel.ProtagonistPersonaSlots
                : viewModel.PartyPersonaSlots;

            if (personaSlots.Count == 0)
            {
                personaSlotChoices.Clear();
                personaEditorControl.SelectedSlot = null;
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
                personaEditorControl.SelectedSlot = personaSlots[selectedPersonaSlotIndex];
            }
            else
            {
                int partyPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1, 0, personaSlots.Count - 1);
                personaSlotChoices.Clear();
                personaEditorControl.SelectedSlot = null;
                personaChoices.Clear();
                personaEditorControl.SelectedPersona = null;
                personaEditorControl.ExperienceText = string.Empty;
                personaEditorControl.Level = 0;
                personaEditorControl.Strength = 0;
                personaEditorControl.Magic = 0;
                personaEditorControl.Endurance = 0;
                personaEditorControl.Agility = 0;
                personaEditorControl.Luck = 0;
                personaSkillChoices1.Clear();
                personaEditorControl.SelectedSkill1 = null;
                personaSkillChoices2.Clear();
                personaEditorControl.SelectedSkill2 = null;
                personaSkillChoices3.Clear();
                personaEditorControl.SelectedSkill3 = null;
                personaSkillChoices4.Clear();
                personaEditorControl.SelectedSkill4 = null;
                personaSkillChoices5.Clear();
                personaEditorControl.SelectedSkill5 = null;
                personaSkillChoices6.Clear();
                personaEditorControl.SelectedSkill6 = null;
                personaSkillChoices7.Clear();
                personaEditorControl.SelectedSkill7 = null;
                personaSkillChoices8.Clear();
                personaEditorControl.SelectedSkill8 = null;
                PersonaSlotViewState partyCurrentSlot = personaSlots[partyPersonaSlotIndex];
                personaChoices.Clear();
                PersonaChoiceViewState partySelectedPersonaChoice = default!;
                foreach (PersonaChoiceViewState choice in SaveEditorViewModel.GetPersonaChoices(
                    partyCurrentSlot.PersonaId,
                    out partySelectedPersonaChoice))
                {
                    personaChoices.Add(choice);
                }
                personaEditorControl.SelectedPersona = partySelectedPersonaChoice;
                personaEditorControl.ExperienceText = partyCurrentSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
                personaEditorControl.Level = partyCurrentSlot.Level;
                personaEditorControl.Strength = partyCurrentSlot.Strength;
                personaEditorControl.Magic = partyCurrentSlot.Magic;
                personaEditorControl.Endurance = partyCurrentSlot.Endurance;
                personaEditorControl.Agility = partyCurrentSlot.Agility;
                personaEditorControl.Luck = partyCurrentSlot.Luck;
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
            personaEditorControl.SelectedPersona = selectedPersonaChoice;
            personaEditorControl.ExperienceText = currentSlot.TotalExperience.ToString(CultureInfo.InvariantCulture);
            personaEditorControl.Level = currentSlot.Level;
            personaEditorControl.Strength = currentSlot.Strength;
            personaEditorControl.Magic = currentSlot.Magic;
            personaEditorControl.Endurance = currentSlot.Endurance;
            personaEditorControl.Agility = currentSlot.Agility;
            personaEditorControl.Luck = currentSlot.Luck;

            SetPersonaSkillChoices(currentSlot.SkillIds);
        }
        finally
        {
            suppressPersonaEvents = false;
            RefreshPersonaSummary();
            UpdatePersonaLevelValueText();
        }
    }

    private static InventoryItemChoiceViewState? SetEquipmentChoices(
        ObservableCollection<InventoryItemChoiceViewState> targetCollection,
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

        return selectedItem;
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
        personaEditorControl.SelectedSkill1 = skill1;
        SetSkillChoices(personaSkillChoices2, skill2Choices);
        personaEditorControl.SelectedSkill2 = skill2;
        SetSkillChoices(personaSkillChoices3, skill3Choices);
        personaEditorControl.SelectedSkill3 = skill3;
        SetSkillChoices(personaSkillChoices4, skill4Choices);
        personaEditorControl.SelectedSkill4 = skill4;
        SetSkillChoices(personaSkillChoices5, skill5Choices);
        personaEditorControl.SelectedSkill5 = skill5;
        SetSkillChoices(personaSkillChoices6, skill6Choices);
        personaEditorControl.SelectedSkill6 = skill6;
        SetSkillChoices(personaSkillChoices7, skill7Choices);
        personaEditorControl.SelectedSkill7 = skill7;
        SetSkillChoices(personaSkillChoices8, skill8Choices);
        personaEditorControl.SelectedSkill8 = skill8;
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
                if (compendiumWorkspacePage is not null)
                {
                    compendiumWorkspacePage.SelectedCompendiumEntry = compendiumItems.FirstOrDefault(
                        entry => entry.SlotIndex == selectedCompendiumSlotIndex.Value);
                }
                personaEditorControl.SelectedMember = null;
                personaEditorControl.SelectedSlot = null;
            }
            else
            {
                if (compendiumWorkspacePage is not null)
                {
                    compendiumWorkspacePage.SelectedCompendiumEntry = selectedCompendiumListSlotIndex.HasValue
                        ? compendiumItems.FirstOrDefault(entry => entry.SlotIndex == selectedCompendiumListSlotIndex.Value)
                        : null;
                }
                personaEditorControl.SelectedMember = selectedPersonaMemberId.HasValue
                    ? personaMemberChoices.FirstOrDefault(member => member.MemberId == selectedPersonaMemberId.Value)
                    : null;
                personaEditorControl.SelectedSlot = selectedPersonaMemberId == 0
                    ? personaSlotChoices.FirstOrDefault(slot => slot.SlotIndex == selectedPersonaSlotIndex)
                    : null;
            }

            if (compendiumWorkspacePage is not null)
            {
                compendiumWorkspacePage.SelectedCompendiumAddChoice = compendiumAddChoices.FirstOrDefault(static choice => choice.PersonaId == 0);
            }
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

        if (inventoryWorkspacePage?.SelectedInventoryEntry is not InventoryStackViewState selectedEntry)
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

        if (inventoryWorkspacePage?.SelectedCategory is ItemCategoryViewState selectedCategory)
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

        if (inventoryWorkspacePage?.SelectedItem is InventoryItemChoiceViewState selectedItem)
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
        string deletedEntryDescription = inventoryWorkspacePage?.SelectedInventoryEntryDescription ?? "the selected inventory entry";
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

        if (personaEditorControl.SelectedMember is not PartyMemberChoiceViewState selectedMember)
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
                if (compendiumWorkspacePage is not null)
                {
                    compendiumWorkspacePage.SelectedCompendiumEntry = null;
                }
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

        if (personaEditorControl.SelectedSlot is PersonaSlotViewState selectedSlot)
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

        if (personaEditorControl.SelectedPersona is PersonaChoiceViewState selectedPersonaChoice)
        {
            double resolvedLevel = ResolvePersonaLevelAfterPersonaChoice(
                selectedPersonaChoice.PersonaId,
                personaEditorControl.Level,
                selectedCompendiumSlotIndex.HasValue);
            if (resolvedLevel != personaEditorControl.Level)
            {
                personaEditorControl.Level = resolvedLevel;
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

        if (equipmentWorkspacePage?.SelectedCharacter is not EquipmentCharacterViewState selectedCharacter)
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
        TrackEquipmentDraftSelection(sender as ComboBox);

    private void EquipmentArmorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackEquipmentDraftSelection(sender as ComboBox);

    private void EquipmentAccessoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackEquipmentDraftSelection(sender as ComboBox);

    private void EquipmentCostumeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackEquipmentDraftSelection(sender as ComboBox);

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
            if (equipmentWorkspacePage is not null)
            {
                equipmentWorkspacePage.SelectedCharacter = GetSelectedEquipmentCharacterViewState();
            }
        }
        finally
        {
            suppressEquipmentEvents = false;
        }
    }

    private void TrackEquipmentDraftSelection(ComboBox? comboBox)
    {
        if (suppressEquipmentEvents ||
            !selectedEquipmentCharacterId.HasValue ||
            comboBox is null ||
            comboBox.SelectedItem is not InventoryItemChoiceViewState)
        {
            return;
        }

        TrackEditorDraft();
    }

    private bool TryReadInventoryQuantity(out byte quantity)
    {
        if (TryReadInventoryQuantityText(inventoryWorkspacePage?.QuantityText ?? string.Empty, out quantity, out SaveDiagnostic diagnostic))
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
        TrackPartyMemberDraftEdit(partyPersonaWorkspacePage?.SelectedPartySlot0);

    private void PartySlot1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackPartyMemberDraftEdit(partyPersonaWorkspacePage?.SelectedPartySlot1);

    private void PartySlot2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        TrackPartyMemberDraftEdit(partyPersonaWorkspacePage?.SelectedPartySlot2);

    private void TrackPartyMemberDraftEdit(PartyConfigurationChoiceViewState? selectedMember)
    {
        if (!CanProcessImmediateEditEvent() ||
            selectedMember is null)
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
        if (compendiumWorkspacePage is not null)
        {
            compendiumWorkspacePage.PersonaSummaryText = BuildPersonaSummary();
            compendiumWorkspacePage.SetEditorContextText(selectedCompendiumSlotIndex.HasValue
                ? "Persona editor is editing the activated compendium row."
                : "The list selection is for browsing; the persona editor stays on the party/protagonist slot until a compendium row is activated.");
        }
    }

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
