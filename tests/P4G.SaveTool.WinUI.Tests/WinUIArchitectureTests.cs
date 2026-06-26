using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class WinUIArchitectureTests
{
    private static readonly string[] ForbiddenBoundaryAssemblyNames =
    [
        "P4G.SaveTool.Domain",
        "P4G.SaveTool.SaveFormat",
        "P4G.SaveTool.Catalog",
    ];

    private static readonly string[] AotCompatibleProductionProjects =
    [
        "P4G.SaveTool.Domain",
        "P4G.SaveTool.Catalog",
        "P4G.SaveTool.Contracts",
        "P4G.SaveTool.SaveFormat",
        "P4G.SaveTool.Application",
        "P4G.SaveTool.Presentation",
    ];

    private static readonly TimeSpan MsBuildEvaluationTimeout = TimeSpan.FromSeconds(60);
    private static readonly Version MinimumCsWinRTAotVersion = new(2, 1, 1);

    private const string CsWinRTPackageName = "Microsoft.Windows.CsWinRT";
    private const string CsWinRTWindowsMetadataPackageName = "Microsoft.Windows.SDK.CPP";
    private const string CsWinRTWindowsMetadataPath = @"$(NuGetPackageRoot)\microsoft.windows.sdk.cpp\$(CsWinRTWindowsMetadataPackageVersion)\c";
    private const string RequiredCsWinRTWindowsMetadataPackageVersion = "10.0.22000.196";
    private const string RequiredCsWinRTWindowsMetadataPlatformVersion = "10.0.22000.0";
    private const string RequiredWindowsSdkPackageVersion = "10.0.22000.57";

    [Fact]
    public void WinUISourceDoesNotReferenceDomainOrSaveFormat()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string[] sourceFiles = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .ToArray();

        Assert.NotEmpty(sourceFiles);
        foreach (string sourceFile in sourceFiles)
        {
            string content = File.ReadAllText(sourceFile);
            foreach (string forbiddenReference in ForbiddenBoundaryAssemblyNames)
            {
                Assert.False(
                    content.Contains(forbiddenReference, StringComparison.Ordinal),
                    $"{sourceFile} must not reference {forbiddenReference}.");
            }
        }
    }

    [Fact]
    public void MainWindowSourceDoesNotReferenceCatalogTypesOrLegacyInventoryIds()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile);

        Assert.DoesNotContain("P4G.SaveTool.Catalog", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemCategoryId", content, StringComparison.Ordinal);
        Assert.DoesNotContain("1792", content, StringComparison.Ordinal);
    }

    [Fact]
    public void InventorySelectionHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        AssertHandlerRefreshesShellState(content, "InventoryListView_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "InventoryCategoryComboBox_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "InventoryItemComboBox_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "EquipmentCharacterComboBox_SelectionChanged", "RefreshEquipmentState();");
        Assert.Contains("ApplyEquipmentSelection(EquipmentWeaponComboBox", content, StringComparison.Ordinal);
        Assert.Contains("ApplyEquipmentSelection(EquipmentArmorComboBox", content, StringComparison.Ordinal);
        Assert.Contains("ApplyEquipmentSelection(EquipmentAccessoryComboBox", content, StringComparison.Ordinal);
        Assert.Contains("ApplyEquipmentSelection(EquipmentCostumeComboBox", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowPersonaHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("PersonaMemberComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("PersonaSlotComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("PersonaChoiceComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("PersonaSkillBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", content, StringComparison.Ordinal);
        Assert.Contains("TryBuildEditBatch(", content, StringComparison.Ordinal);
        Assert.Contains("SetProtagonistPersonaSlotEdit", content, StringComparison.Ordinal);
        Assert.Contains("SetPartyPersonaSlotEdit", content, StringComparison.Ordinal);
        Assert.Contains("int partyPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1, 0, personaSlots.Count - 1);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSocialLinkHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string applyBody = GetSection(
            content,
            "private void SocialLinkApplyButton_Click(object sender, RoutedEventArgs e)",
            "private void SocialLinkDeleteButton_Click(");
        string deleteBody = GetSection(
            content,
            "private void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e)",
            "private void RefreshEquipmentState()");
        string addBody = GetSection(
            content,
            "private void SocialLinkAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void SocialLinkApplyButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("RefreshSocialLinksState();", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkListView_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkAddComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkApplyButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDeleteButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("RefreshSocialLinkDraftPreservingSelection(", addBody, StringComparison.Ordinal);
        Assert.Contains("RefreshSocialLinkDraftPreservingSelection(", deleteBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryAppendSelectedSocialLinkEdits(edits, validationDiagnostics))", applyBody, StringComparison.Ordinal);
        Assert.Contains("SetUiDiagnostics(validationDiagnostics);", applyBody, StringComparison.Ordinal);
        Assert.Contains("viewModel.ApplyEdits(edits)", applyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkLevel(", applyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkProgress(", applyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkFlag(", applyBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", addBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", deleteBody, StringComparison.Ordinal);
        Assert.Contains("AddSocialLink(", content, StringComparison.Ordinal);
        Assert.Contains("RemoveSocialLink(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("viewModel.SocialLinks.Any(link => link.LinkId == selectedChoice.LinkId)", addBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlDeclaresSocialLinkEditingControls()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"SocialLinkListView\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"SocialLinkListView_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkAddComboBox\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"SocialLinkAddComboBox_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkLevelTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkProgressTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkFlagTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkApplyButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"SocialLinkApplyButton_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkDeleteButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"SocialLinkDeleteButton_Click\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUpdateShellStateEnablesAndDisablesSocialLinkEditorControls()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string updateShellStateBody = GetSection(
            content,
            "private void UpdateShellState()",
            "private void RefreshSocialStatsState()");

        Assert.Contains("SocialLinkListView.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkAddComboBox.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkLevelTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkProgressTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkFlagTextBox.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkApplyButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDeleteButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowPersonaSummaryRefreshesWithPersonaSelectionState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string refreshPersonaStateBody = GetSection(
            content,
            "private void RefreshPersonaState()",
            "private void PersonaMemberComboBox_SelectionChanged(");
        string memberHandlerBody = GetSection(
            content,
            "private void PersonaMemberComboBox_SelectionChanged(",
            "private void PersonaSlotComboBox_SelectionChanged(");

        Assert.Contains("RefreshPersonaSummary();", refreshPersonaStateBody, StringComparison.Ordinal);
        Assert.Contains("selectedPersonaMemberId = null;", memberHandlerBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaSummary();", memberHandlerBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowEquipmentEditsRefreshOnlyEquipmentState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void ApplyEquipmentSelection(",
            "private bool TryReadInventoryQuantity(");
        string propertyChangedBody = GetSection(
            content,
            "private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)",
            "private async Task RunBusyAsync(Func<Task<BusyOperationCompletion>> operation)");

        Assert.Contains("RefreshEquipmentState();", methodBody, StringComparison.Ordinal);
        Assert.Contains("DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);", methodBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModel();", methodBody, StringComparison.Ordinal);
        Assert.Contains("preservePersonaEditorStateDuringEquipmentRefresh", methodBody, StringComparison.Ordinal);
        Assert.Contains("if (preservePersonaEditorStateDuringEquipmentRefresh)", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("RefreshEquipmentState();", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("return;", propertyChangedBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowInventoryEditsPreserveEditorTextDuringRefresh()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string propertyChangedBody = GetSection(
            content,
            "private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)",
            "private async Task RunBusyAsync(Func<Task<BusyOperationCompletion>> operation)");
        string addUpdateBody = GetSection(
            content,
            "private void InventoryAddUpdateButton_Click(object sender, RoutedEventArgs e)",
            "private void InventoryDeleteButton_Click(");
        string deleteBody = GetSection(
            content,
            "private void InventoryDeleteButton_Click(object sender, RoutedEventArgs e)",
            "private void PersonaMemberComboBox_SelectionChanged(");

        Assert.Contains("if (preserveEditorTextDuringInventoryRefresh)", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("preserveEditorTextDuringInventoryRefresh = true;", addUpdateBody, StringComparison.Ordinal);
        Assert.Contains("preserveEditorTextDuringInventoryRefresh = false;", addUpdateBody, StringComparison.Ordinal);
        Assert.Contains("preserveEditorTextDuringInventoryRefresh = true;", deleteBody, StringComparison.Ordinal);
        Assert.Contains("preserveEditorTextDuringInventoryRefresh = false;", deleteBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSaveValidationFailurePreservesEditorState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string runBusyBody = GetSection(
            content,
            "private async Task RunBusyAsync(Func<Task<BusyOperationCompletion>> operation)",
            "private async Task<BusyOperationCompletion> OpenSaveFileAsync()");
        string propertyChangedBody = GetSection(
            content,
            "private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)",
            "private async Task RunBusyAsync(Func<Task<BusyOperationCompletion>> operation)");
        string applyEditorFieldsBody = GetSection(
            content,
            "private bool ApplyEditorFields()",
            "private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)");
        string saveBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)",
            "private async Task<string?> PickSavePathAsync()");
        string inventoryRefreshBody = GetSection(
            content,
            "private void RefreshFromViewModelPreservingInventoryQuantityDraft(bool preserveSelectedSocialLinkDraft = true)",
            "private void RefreshEditableFields()");

        Assert.Contains("BusyOperationCompletion completion = BusyOperationCompletion.RefreshViewModel;", runBusyBody, StringComparison.Ordinal);
        Assert.Contains("if (completion == BusyOperationCompletion.RefreshViewModel)", runBusyBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", runBusyBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.IsFullRefreshSuppressed", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("ShouldPreserveSelectedSocialLinkDraftAfterApply(edits)", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("RefreshFromViewModelPreservingInventoryQuantityDraft(", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModelPreservingInventoryQuantityDraft();", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("if (!ApplyEditorFields())", saveBody, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Count(saveBody, Regex.Escape("return BusyOperationCompletion.PreserveEditorState;")));
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", saveBody, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(saveBody, Regex.Escape("RefreshFromViewModelPreservingInventoryQuantityDraft();")));
        Assert.Contains("string inventoryQuantityDraft = InventoryQuantityTextBox.Text;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDraftState? socialLinkDraft = CaptureSelectedSocialLinkDraft();", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("InventorySelectionState.ShouldRestoreQuantityDraft(", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("InventoryQuantityTextBox.Text = inventoryQuantityDraft;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("if (preserveSelectedSocialLinkDraft && socialLinkDraft is not null)", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("RestoreSelectedSocialLinkDraft(socialLinkDraft.Value);", inventoryRefreshBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUnrelatedGroup4ApplyAndSavePreserveInventoryQuantityDrafts()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string applyEditorFieldsBody = GetSection(
            content,
            "private bool ApplyEditorFields()",
            "private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)");
        string saveBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)",
            "private async Task<string?> PickSavePathAsync()");

        Assert.Contains("RefreshFromViewModelPreservingInventoryQuantityDraft(", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModelPreservingInventoryQuantityDraft();", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(saveBody, Regex.Escape("RefreshFromViewModelPreservingInventoryQuantityDraft();")));
    }

    [Fact]
    public void MainWindowZeroQuantityInventoryUpdatesSuppressAutoSelectLikeDelete()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void InventoryAddUpdateButton_Click(",
            "private void InventoryDeleteButton_Click(");

        Assert.Contains("if (quantity == 0)", methodBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowAutoSelectsInventoryEntryOnOpenAndSuppressesItAfterDelete()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("inventorySelectionState.Reset();", content, StringComparison.Ordinal);
        Assert.Contains("autoSelectInventoryEntryAfterOpen = true;", content, StringComparison.Ordinal);
        Assert.Contains("ShouldAutoSelectFirstEntry(", content, StringComparison.Ordinal);
        Assert.Contains("selectedEntry = viewModel.InventoryEntries[lastInventoryEntryIndex];", content, StringComparison.Ordinal);
        Assert.Contains("int lastInventoryEntryIndex = viewModel.InventoryEntries.Count - 1;", content, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowOpenAndResetClearSelectedSocialLinkLinkId()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string openBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> OpenSaveFileAsync()",
            "private bool ApplyEditorFields()");
        string refreshSocialLinksBody = GetSection(
            content,
            "private void RefreshSocialLinksState()",
            "private void RefreshInventoryState()");

        Assert.Contains("selectedSocialLinkIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedSocialLinkLinkId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);", refreshSocialLinksBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlExposesInventoryEditorSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"InventoryListView\"", xaml);
        Assert.Contains("x:Name=\"InventoryCategoryComboBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryItemComboBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryQuantityTextBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryAddUpdateButton\"", xaml);
        Assert.Contains("x:Name=\"InventoryDeleteButton\"", xaml);
        Assert.Contains("Text=\"{Binding DisplayName}\"", xaml);
        Assert.Contains("x:Name=\"EquipmentCharacterComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentWeaponComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentArmorComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentAccessoryComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentCostumeComboBox\"", xaml);
    }

    [Fact]
    public void MainWindowXamlExposesPersonaEditorSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"PersonaMemberComboBox\"", xaml);
        Assert.Contains("x:Name=\"PersonaSlotComboBox\"", xaml);
        Assert.Contains("x:Name=\"PersonaChoiceComboBox\"", xaml);
        Assert.Contains("x:Name=\"PersonaXpTextBox\"", xaml);
        Assert.Contains("x:Name=\"PersonaLevelSlider\"", xaml);
        Assert.Contains("x:Name=\"PersonaStrengthSlider\"", xaml);
        Assert.Contains("x:Name=\"PersonaMagicSlider\"", xaml);
        Assert.Contains("x:Name=\"PersonaEnduranceSlider\"", xaml);
        Assert.Contains("x:Name=\"PersonaAgilitySlider\"", xaml);
        Assert.Contains("x:Name=\"PersonaLuckSlider\"", xaml);
        Assert.Contains("x:Name=\"PersonaSkillBox1\"", xaml);
        Assert.Contains("x:Name=\"PersonaSkillBox8\"", xaml);
    }

    [Fact]
    public void MainWindowXamlExposesSocialStatsAndCalendarEditorSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"CourageComboBox\"", xaml);
        Assert.Contains("x:Name=\"KnowledgeComboBox\"", xaml);
        Assert.Contains("x:Name=\"ExpressionComboBox\"", xaml);
        Assert.Contains("x:Name=\"UnderstandingComboBox\"", xaml);
        Assert.Contains("x:Name=\"DiligenceComboBox\"", xaml);
        Assert.Contains("x:Name=\"DayTextBox\"", xaml);
        Assert.Contains("x:Name=\"PhaseComboBox\"", xaml);
        Assert.Contains("x:Name=\"NextDayTextBox\"", xaml);
        Assert.Contains("x:Name=\"NextPhaseComboBox\"", xaml);
    }

    [Fact]
    public void MainWindowXamlWiresApplyButtonForSocialStatsAndCalendarEdits()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"ApplyButton\"", xaml);
        Assert.Contains("Click=\"ApplyButton_Click\"", xaml);
        Assert.Contains("x:Name=\"CourageComboBox\"", xaml);
        Assert.Contains("x:Name=\"DayTextBox\"", xaml);
        Assert.Contains("x:Name=\"NextPhaseComboBox\"", xaml);
    }

    [Fact]
    public void MainWindowSourceBindsSocialStatsAndCalendarEditsThroughPresentationState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string tryBuildEditBatchBody = GetSection(
            content,
            "private bool TryBuildEditBatch(",
            "private static void AddPartyMemberValue(");
        string tryBuildSocialLinkEditsBody = GetSection(
            content,
            "internal static bool TryBuildSocialLinkEdits(",
            "internal static bool TryReadSocialLinkField(");
        string tryReadSocialLinkFieldBody = GetSection(
            content,
            "internal static bool TryReadSocialLinkField(",
            "private string GetPartyMemberValue(int slotIndex)");

        Assert.Contains("Group4EditBatchBuilder.TryBuild(", content, StringComparison.Ordinal);
        Assert.Contains("CreateGroup4EditInputs(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("AppendGroup4Edits(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("TryAppendSelectedSocialLinkEdits(batch, validationDiagnostics);", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("TryFinalizeEditBatch(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new Group4EditInputs(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("bool levelIsValid = TryReadSocialLinkField(levelText, \"Level\", \"SocialLinks.Level\", diagnostics, out byte level);", content, StringComparison.Ordinal);
        Assert.Contains("bool progressIsValid = TryReadSocialLinkField(progressText, \"Progress\", \"SocialLinks.Progress\", diagnostics, out byte progress);", content, StringComparison.Ordinal);
        Assert.Contains("bool flagIsValid = TryReadSocialLinkField(flagText, \"Flag\", \"SocialLinks.Flag\", diagnostics, out byte flag);", content, StringComparison.Ordinal);
        Assert.Contains("if (!levelIsValid || !progressIsValid || !flagIsValid)", tryBuildSocialLinkEditsBody, StringComparison.Ordinal);
        Assert.Contains("diagnostics.Add(CreateUiDiagnostic(\"P4GWINUI024\",", tryReadSocialLinkFieldBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetUiDiagnostics(", tryReadSocialLinkFieldBody, StringComparison.Ordinal);
        Assert.Contains("CourageComboBox.SelectedItem as SocialStatRankChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("KnowledgeComboBox.SelectedItem as SocialStatRankChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("ExpressionComboBox.SelectedItem as SocialStatRankChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("UnderstandingComboBox.SelectedItem as SocialStatRankChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("DiligenceComboBox.SelectedItem as SocialStatRankChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("DayTextBox.Text ?? string.Empty", content, StringComparison.Ordinal);
        Assert.Contains("PhaseComboBox.SelectedItem as CalendarPhaseChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("NextDayTextBox.Text ?? string.Empty", content, StringComparison.Ordinal);
        Assert.Contains("NextPhaseComboBox.SelectedItem as CalendarPhaseChoiceViewState", content, StringComparison.Ordinal);
        Assert.Contains("RefreshSocialStatsState();", content, StringComparison.Ordinal);
        Assert.Contains("RefreshCalendarState();", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRefreshEditableFieldsBindsGroup4SocialStatsAndCalendarSlots()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string refreshEditableFieldsBody = GetSection(
            content,
            "private void RefreshEditableFields()",
            "private void UpdateShellState()");
        string refreshSocialStatsBody = GetSection(
            content,
            "private void RefreshSocialStatsState()",
            "private void SetSocialStatSelection(");
        string refreshCalendarBody = GetSection(
            content,
            "private void RefreshCalendarState()",
            "private void RefreshInventoryState()");

        Assert.Contains("RefreshSocialStatsState();", refreshEditableFieldsBody, StringComparison.Ordinal);
        Assert.Contains("RefreshCalendarState();", refreshEditableFieldsBody, StringComparison.Ordinal);

        Assert.Contains("SetSocialStatSelection(CourageComboBox, 0);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(KnowledgeComboBox, 1);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(ExpressionComboBox, 4);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(UnderstandingComboBox, 3);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(DiligenceComboBox, 2);", refreshSocialStatsBody, StringComparison.Ordinal);

        Assert.Contains("DayTextBox.Text = viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture);", refreshCalendarBody, StringComparison.Ordinal);
        Assert.Contains("NextDayTextBox.Text = viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture);", refreshCalendarBody, StringComparison.Ordinal);
        Assert.Contains(
            "PhaseComboBox.ItemsSource = viewModel.GetCalendarPhaseChoices(viewModel.Calendar.DayPhaseId, out CalendarPhaseChoiceViewState selectedPhase);",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "NextPhaseComboBox.ItemsSource = viewModel.GetCalendarPhaseChoices(viewModel.Calendar.NextDayPhaseId, out CalendarPhaseChoiceViewState selectedNextPhase);",
            refreshCalendarBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WinUIProjectDoesNotDirectlyReferenceDomainOrSaveFormat()
    {
        string projectFile = Path.Combine(
            FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"),
            "P4G.SaveTool.WinUI.csproj");
        XDocument project = XDocument.Load(projectFile);

        string[] directReferences = project
            .Descendants()
            .Where(static element => element.Name.LocalName is "ProjectReference" or "Reference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .Select(GetReferencedAssemblyName)
            .ToArray();

        Assert.NotEmpty(directReferences);
        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(forbiddenAssemblyName, directReferences);
        }
    }

    [Fact]
    public void WinUIAssemblyDoesNotDirectlyReferenceDomainOrSaveFormat()
    {
        string[] referencedAssemblies = typeof(MainWindow)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assemblyName => assemblyName.Name)
            .Where(static name => name is not null)
            .Select(static name => name!)
            .ToArray();

        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(forbiddenAssemblyName, referencedAssemblies);
        }
    }

    [Fact]
    public void PartyMemberViewStateDoesNotExposeDomainOrSaveFormatTypes()
    {
        Type[] exposedPropertyTypes = typeof(PartyMemberSlotViewState)
            .GetProperties()
            .Select(static property => property.PropertyType)
            .ToArray();

        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(
                exposedPropertyTypes,
                propertyType => propertyType.Assembly.GetName().Name == forbiddenAssemblyName);
        }
    }

    [Fact]
    public void EquipmentCharacterViewStateDoesNotExposeDomainOrSaveFormatTypes()
    {
        Type[] exposedPropertyTypes = typeof(EquipmentCharacterViewState)
            .GetProperties()
            .Select(static property => property.PropertyType)
            .ToArray();

        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(
                exposedPropertyTypes,
                propertyType => propertyType.Assembly.GetName().Name == forbiddenAssemblyName);
        }
    }

    [Fact]
    public void WinUIProjectDeclaresUnpackagedNativeAotPublishSettings()
    {
        XDocument project = XDocument.Load(GetWinUIProjectFile());

        AssertProperty(project, "UseWinUI", "true");
        AssertProperty(project, "WindowsPackageType", "None");
        AssertProperty(project, "Platforms", "x64");
        AssertProperty(project, "RuntimeIdentifier", "win-x64");
        AssertProperty(project, "SelfContained", "true");
        AssertProperty(project, "WindowsAppSDKSelfContained", "true");
        AssertProperty(project, "PublishAot", "true");
        AssertProperty(project, "WindowsSdkPackageVersion", RequiredWindowsSdkPackageVersion);
        AssertProperty(project, "CsWinRTWindowsMetadataPackageVersion", RequiredCsWinRTWindowsMetadataPackageVersion);
        AssertProperty(project, "CsWinRTWindowsMetadata", CsWinRTWindowsMetadataPath);
        AssertNoTrueProperty(project, "PublishSingleFile");
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName == "RuntimeIdentifiers");
    }

    [Fact]
    public void WinUIProjectReferencesCsWinRTSourceGeneratorForNativeAot()
    {
        XDocument project = XDocument.Load(GetWinUIProjectFile());
        string[] directPackageReferences = GetPackageIncludes(project, "PackageReference");
        Assert.Contains(CsWinRTPackageName, directPackageReferences);
        Assert.Contains(CsWinRTWindowsMetadataPackageName, directPackageReferences);

        XDocument centralPackages = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"));
        string packageVersion = GetRequiredPackageVersion(centralPackages, CsWinRTPackageName);
        Version parsedVersion = ParsePackageVersion(packageVersion, CsWinRTPackageName);
        Assert.True(
            parsedVersion.CompareTo(MinimumCsWinRTAotVersion) >= 0,
            $"{CsWinRTPackageName} must be at least {MinimumCsWinRTAotVersion} for WinUI NativeAOT source generation.");
        Assert.Equal(
            RequiredCsWinRTWindowsMetadataPackageVersion,
            GetRequiredPackageVersion(centralPackages, CsWinRTWindowsMetadataPackageName));
    }

    [Fact]
    public void NativeAotPublishProfileDeclaresStableFolderPublishSettings()
    {
        string winUIProjectDirectory = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string publishProfile = Path.Combine(
            winUIProjectDirectory,
            "Properties",
            "PublishProfiles",
            "nativeaot-win-x64.pubxml");
        XDocument profile = XDocument.Load(publishProfile);

        AssertProperty(profile, "Configuration", "Release");
        AssertProperty(profile, "Platform", "x64");
        AssertProperty(profile, "RuntimeIdentifier", "win-x64");
        AssertProperty(profile, "PublishAot", "true");
        AssertProperty(profile, "SelfContained", "true");
        AssertProperty(profile, "WindowsAppSDKSelfContained", "true");
        AssertNoTrueProperty(profile, "PublishSingleFile");

        string expectedPublishDirectory = NormalizeDirectoryPath(Path.Combine(
            winUIProjectDirectory,
            "..",
            "..",
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64"));
        string publishDirectory = EvaluatePublishDirectory(
            GetRequiredPropertyValue(profile, "PublishDir"),
            winUIProjectDirectory);
        string actualPublishDirectory = NormalizeDirectoryPath(publishDirectory);
        Assert.True(
            string.Equals(expectedPublishDirectory, actualPublishDirectory, StringComparison.OrdinalIgnoreCase),
            $"PublishDir must resolve to '{expectedPublishDirectory}' but resolved to '{actualPublishDirectory}'.");
    }

    [Fact]
    public void ProductionLibraryProjectsAreMarkedAotCompatible()
    {
        foreach (string projectName in AotCompatibleProductionProjects)
        {
            string projectFile = Path.Combine(
                FindRepositoryDirectory("src", projectName),
                $"{projectName}.csproj");
            XDocument project = XDocument.Load(projectFile);

            AssertProperty(project, "IsAotCompatible", "true");
        }
    }

    [Fact]
    public async Task NativeAotPublishProfileDefaultsEvaluateWithoutExplicitGlobals()
    {
        IReadOnlyDictionary<string, string> properties = await GetEvaluatedPropertiesAsync(
            GetWinUIProjectFile(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PublishProfile"] = "nativeaot-win-x64",
            },
            [
                "Configuration",
                "Platform",
                "RuntimeIdentifier",
                "WindowsPackageType",
                "SelfContained",
                "WindowsAppSDKSelfContained",
                "PublishAot",
                "PublishSingleFile",
                "PublishDir",
                "WindowsSdkPackageVersion",
                "CsWinRTWindowsMetadataPackageVersion",
                "CsWinRTWindowsMetadata",
                "NuGetPackageRoot",
            ]);
 
        AssertEvaluatedProperty(properties, "Configuration", "Release");
        AssertEvaluatedProperty(properties, "Platform", "x64");
        AssertEvaluatedProperty(properties, "RuntimeIdentifier", "win-x64");
        AssertEvaluatedProperty(properties, "WindowsPackageType", "None");
        AssertEvaluatedProperty(properties, "SelfContained", "true");
        AssertEvaluatedProperty(properties, "WindowsAppSDKSelfContained", "true");
        AssertEvaluatedProperty(properties, "PublishAot", "true");
        AssertEvaluatedFalseOrEmptyProperty(properties, "PublishSingleFile");
        AssertEvaluatedWindowsSdkMetadataProperties(properties);

        string expectedPublishDirectory = NormalizeDirectoryPath(Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64"));
        string actualPublishDirectory = NormalizeDirectoryPath(
            GetRequiredEvaluatedProperty(properties, "PublishDir"));
        Assert.True(
            string.Equals(expectedPublishDirectory, actualPublishDirectory, StringComparison.OrdinalIgnoreCase),
            $"PublishDir must evaluate to '{expectedPublishDirectory}' but evaluated to '{actualPublishDirectory}'.");
    }

    [Fact]
    public async Task NativeAotPublishProfileEvaluatesExpectedPublishSettings()
    {
        IReadOnlyDictionary<string, string> properties = await GetEvaluatedPropertiesAsync(
            GetWinUIProjectFile(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = "Release",
                ["Platform"] = "x64",
                ["RuntimeIdentifier"] = "win-x64",
                ["PublishProfile"] = "nativeaot-win-x64",
            },
            [
                "WindowsPackageType",
                "SelfContained",
                "WindowsAppSDKSelfContained",
                "PublishAot",
                "PublishSingleFile",
                "RuntimeIdentifier",
                "Platform",
                "PublishDir",
                "WindowsSdkPackageVersion",
                "CsWinRTWindowsMetadataPackageVersion",
                "CsWinRTWindowsMetadata",
                "NuGetPackageRoot",
            ]);

        AssertEvaluatedProperty(properties, "WindowsPackageType", "None");
        AssertEvaluatedProperty(properties, "SelfContained", "true");
        AssertEvaluatedProperty(properties, "WindowsAppSDKSelfContained", "true");
        AssertEvaluatedProperty(properties, "PublishAot", "true");
        AssertEvaluatedFalseOrEmptyProperty(properties, "PublishSingleFile");
        AssertEvaluatedProperty(properties, "RuntimeIdentifier", "win-x64");
        AssertEvaluatedProperty(properties, "Platform", "x64");
        AssertEvaluatedWindowsSdkMetadataProperties(properties);

        string expectedPublishDirectory = NormalizeDirectoryPath(Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64"));
        string actualPublishDirectory = NormalizeDirectoryPath(
            GetRequiredEvaluatedProperty(properties, "PublishDir"));
        Assert.True(
            string.Equals(expectedPublishDirectory, actualPublishDirectory, StringComparison.OrdinalIgnoreCase),
            $"PublishDir must evaluate to '{expectedPublishDirectory}' but evaluated to '{actualPublishDirectory}'.");
    }

    [Fact]
    public async Task ProductionLibraryProjectsEvaluateAotCompatible()
    {
        IReadOnlyDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = "Release",
            ["Platform"] = "x64",
            ["RuntimeIdentifier"] = "win-x64",
        };

        foreach (string projectName in AotCompatibleProductionProjects)
        {
            string projectFile = Path.Combine(
                FindRepositoryDirectory("src", projectName),
                $"{projectName}.csproj");
            IReadOnlyDictionary<string, string> properties = await GetEvaluatedPropertiesAsync(
                projectFile,
                globalProperties,
                ["IsAotCompatible"]);

            AssertEvaluatedProperty(properties, "IsAotCompatible", "true");
        }
    }

    private static string FindRepositoryDirectory(params string[] relativePathSegments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativePathSegments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find {Path.Combine(relativePathSegments)} from {AppContext.BaseDirectory}.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "P4G.SaveTool.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find P4G.SaveTool.sln from {AppContext.BaseDirectory}.");
    }

    private static string GetSection(string content, string startMarker, string endMarker)
    {
        int startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Could not find start marker '{startMarker}'.");

        int endIndex = content.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Could not find end marker '{endMarker}'.");

        return content.Substring(startIndex, endIndex - startIndex);
    }

    private static string GetReferencedAssemblyName(string include)
    {
        string reference = include.Split(',', 2)[0];
        return string.Equals(Path.GetExtension(reference), ".csproj", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(reference)
            : reference;
    }

    private static void AssertHandlerRefreshesShellState(string content, string methodName, string refreshMethodName)
    {
        string methodHeader = $"    private void {methodName}(object sender, SelectionChangedEventArgs e)";
        int methodStart = content.IndexOf(methodHeader, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"{methodName} was not found.");

        int nextMethodStart = content.IndexOf("\n    private ", methodStart + methodHeader.Length, StringComparison.Ordinal);
        string methodBody = nextMethodStart >= 0
            ? content.Substring(methodStart, nextMethodStart - methodStart)
            : content[methodStart..];

        int refreshIndex = methodBody.IndexOf(refreshMethodName, StringComparison.Ordinal);
        int shellIndex = refreshIndex >= 0
            ? methodBody.IndexOf("UpdateShellState();", refreshIndex, StringComparison.Ordinal)
            : -1;

        Assert.True(refreshIndex >= 0, $"{methodName} must refresh state.");
        Assert.True(shellIndex > refreshIndex, $"{methodName} must update shell state after refreshing state.");
    }

    private static string GetWinUIProjectFile()
    {
        return Path.Combine(
            FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"),
            "P4G.SaveTool.WinUI.csproj");
    }

    private static void AssertProperty(XDocument project, string propertyName, string expectedValue)
    {
        string actualValue = GetRequiredPropertyValue(project, propertyName);
        Assert.Equal(expectedValue, actualValue);
    }

    private static void AssertNoTrueProperty(XDocument project, string propertyName)
    {
        string[] actualValues = GetPropertyValues(project, propertyName);
        Assert.True(
            actualValues.Length <= 1,
            $"{propertyName} must not be declared more than once. Found values: {FormatPropertyValues(actualValues)}.");
        Assert.False(
            actualValues.Any(static actualValue => string.Equals(actualValue, "true", StringComparison.OrdinalIgnoreCase)),
            $"{propertyName} must not be set to true.");
    }

    private static void AssertEvaluatedProperty(
        IReadOnlyDictionary<string, string> properties,
        string propertyName,
        string expectedValue)
    {
        string actualValue = GetRequiredEvaluatedProperty(properties, propertyName);
        Assert.Equal(expectedValue, actualValue);
    }

    private static void AssertEvaluatedFalseOrEmptyProperty(
        IReadOnlyDictionary<string, string> properties,
        string propertyName)
    {
        string actualValue = GetRequiredEvaluatedProperty(properties, propertyName);
        Assert.True(
            string.IsNullOrWhiteSpace(actualValue)
            || string.Equals(actualValue, "false", StringComparison.OrdinalIgnoreCase),
            $"{propertyName} must evaluate empty or false, but evaluated to '{actualValue}'.");
    }

    private static string GetRequiredPropertyValue(XDocument project, string propertyName)
    {
        string[] values = GetPropertyValues(project, propertyName);
        Assert.True(values.Length > 0, $"Expected {propertyName} to be set.");
        Assert.True(
            values.Length == 1,
            $"{propertyName} must be declared exactly once. Found values: {FormatPropertyValues(values)}.");
        return values[0];
    }

    private static string[] GetPropertyValues(XDocument project, string propertyName)
    {
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == propertyName)
            .Select(static element => element.Value.Trim())
            .ToArray();
    }

    private static string[] GetPackageIncludes(XDocument project, string itemName)
    {
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == itemName)
            .Select(static element => ((string?)element.Attribute("Include"))?.Trim())
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .ToArray();
    }

    private static string GetRequiredPackageVersion(XDocument centralPackages, string packageName)
    {
        string[] packageVersions = centralPackages
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion"
                && string.Equals((string?)element.Attribute("Include"), packageName, StringComparison.OrdinalIgnoreCase))
            .Select(static element => ((string?)element.Attribute("Version"))?.Trim())
            .Where(static version => !string.IsNullOrWhiteSpace(version))
            .Select(static version => version!)
            .ToArray();

        Assert.True(
            packageVersions.Length == 1,
            $"Expected exactly one central {packageName} version. Found values: {FormatPropertyValues(packageVersions)}.");
        return packageVersions[0];
    }

    private static Version ParsePackageVersion(string packageVersion, string packageName)
    {
        string stableVersion = packageVersion.Split('-', 2)[0];
        Assert.True(
            Version.TryParse(stableVersion, out Version? parsedVersion),
            $"Expected {packageName} version '{packageVersion}' to be a valid semantic version.");
        return parsedVersion;
    }

    private static string GetRequiredEvaluatedProperty(
        IReadOnlyDictionary<string, string> properties,
        string propertyName)
    {
        Assert.True(
            properties.TryGetValue(propertyName, out string? value),
            $"Expected MSBuild to return {propertyName}. Returned properties: {string.Join(", ", properties.Keys)}.");
        return value;
    }

    private static void AssertEvaluatedWindowsSdkMetadataProperties(
        IReadOnlyDictionary<string, string> properties)
    {
        AssertEvaluatedProperty(properties, "WindowsSdkPackageVersion", RequiredWindowsSdkPackageVersion);
        AssertEvaluatedProperty(
            properties,
            "CsWinRTWindowsMetadataPackageVersion",
            RequiredCsWinRTWindowsMetadataPackageVersion);

        string nugetPackageRoot = NormalizeDirectoryPath(GetRequiredEvaluatedProperty(properties, "NuGetPackageRoot"));
        string expectedMetadataRoot = NormalizeDirectoryPath(Path.Combine(
            nugetPackageRoot,
            "microsoft.windows.sdk.cpp",
            RequiredCsWinRTWindowsMetadataPackageVersion,
            "c"));
        string actualMetadataRoot = NormalizeDirectoryPath(GetRequiredEvaluatedProperty(
            properties,
            "CsWinRTWindowsMetadata"));

        Assert.False(
            actualMetadataRoot.Contains("$(", StringComparison.Ordinal),
            $"CsWinRTWindowsMetadata must be fully expanded but evaluated to '{actualMetadataRoot}'.");
        Assert.True(
            string.Equals(expectedMetadataRoot, actualMetadataRoot, StringComparison.OrdinalIgnoreCase),
            $"CsWinRTWindowsMetadata must evaluate to '{expectedMetadataRoot}' but evaluated to '{actualMetadataRoot}'.");
        Assert.DoesNotContain(
            Path.Combine("Windows Kits", "10"),
            actualMetadataRoot,
            StringComparison.OrdinalIgnoreCase);

        string platformMetadataFile = Path.Combine(
            actualMetadataRoot,
            "Platforms",
            "UAP",
            RequiredCsWinRTWindowsMetadataPlatformVersion,
            "Platform.xml");
        Assert.True(
            File.Exists(platformMetadataFile),
            $"CsWinRTWindowsMetadata must resolve to a NuGet Windows SDK package root containing {platformMetadataFile}.");
    }

    private static async Task<IReadOnlyDictionary<string, string>> GetEvaluatedPropertiesAsync(
        string projectFile,
        IReadOnlyDictionary<string, string> globalProperties,
        string[] propertyNames)
    {
        Assert.NotEmpty(propertyNames);

        ProcessStartInfo startInfo = new(GetDotNetExecutable())
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectFile);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-v:quiet");
        startInfo.ArgumentList.Add($"-getProperty:{string.Join(",", propertyNames)}");
        foreach (KeyValuePair<string, string> globalProperty in globalProperties)
        {
            startInfo.ArgumentList.Add($"-p:{globalProperty.Key}={globalProperty.Value}");
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        Task waitTask = process.WaitForExitAsync();
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(MsBuildEvaluationTimeout)).ConfigureAwait(false);
        if (completedTask != waitTask)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail(
                $"dotnet msbuild did not complete within {MsBuildEvaluationTimeout.TotalSeconds} seconds for {projectFile}.");
        }

        await waitTask.ConfigureAwait(false);
        string standardOutput = await standardOutputTask.ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);

        Assert.True(
            process.ExitCode == 0,
            $"dotnet msbuild failed for {projectFile} with exit code {process.ExitCode}."
            + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}"
            + $"{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");

        return ParseMsBuildProperties(standardOutput, propertyNames);
    }

    private static Dictionary<string, string> ParseMsBuildProperties(
        string standardOutput,
        string[] propertyNames)
    {
        string trimmedOutput = standardOutput.Trim();
        if (propertyNames.Length == 1)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [propertyNames[0]] = trimmedOutput,
            };
        }

        using JsonDocument document = JsonDocument.Parse(trimmedOutput);
        JsonElement propertiesElement = document.RootElement.GetProperty("Properties");
        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in propertiesElement.EnumerateObject())
        {
            properties[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return properties;
    }

    private static string GetDotNetExecutable()
    {
        string? dotNetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return string.IsNullOrWhiteSpace(dotNetHostPath)
            ? "dotnet"
            : dotNetHostPath;
    }

    private static string EvaluatePublishDirectory(string publishDirectory, string projectDirectory)
    {
        string expandedPublishDirectory = publishDirectory.Replace(
            "$(MSBuildProjectDirectory)",
            projectDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            expandedPublishDirectory.Contains("$(", StringComparison.Ordinal),
            $"PublishDir contains unevaluated MSBuild properties: {publishDirectory}");
        return expandedPublishDirectory;
    }

    private static string NormalizeDirectoryPath(string directoryPath)
    {
        string fullPath = Path.GetFullPath(directoryPath);
        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static string FormatPropertyValues(string[] values)
    {
        return values.Length == 0
            ? "<none>"
            : string.Join(", ", values.Select(static value => $"'{value}'"));
    }
}
