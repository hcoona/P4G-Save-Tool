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
    private const string SettingsControlsPackageName = "CommunityToolkit.WinUI.Controls.SettingsControls";
    private const string CsWinRTWindowsMetadataPath = @"$(NuGetPackageRoot)\microsoft.windows.sdk.cpp\$(CsWinRTWindowsMetadataPackageVersion)\c";
    private const string RequiredCsWinRTWindowsMetadataPackageVersion = "10.0.22000.196";
    private const string RequiredCsWinRTWindowsMetadataPlatformVersion = "10.0.22000.0";
    private const string RequiredWindowsSdkPackageVersion = "10.0.22000.57";
    private const string LegacyWpfAssemblyName = "P4G Save Tool";

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
        string listHandlerBody = GetSection(
            content,
            "private void InventoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void InventoryCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string categoryHandlerBody = GetSection(
            content,
            "private void InventoryCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void InventoryItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string itemHandlerBody = GetSection(
            content,
            "private void InventoryItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void InventoryAddUpdateButton_Click(object sender, RoutedEventArgs e)");

        AssertHandlerRefreshesShellState(content, "InventoryListView_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "InventoryCategoryComboBox_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "InventoryItemComboBox_SelectionChanged", "RefreshInventoryState();");
        AssertInventoryHandlerGuardsDraftBeforeClearingDraft(listHandlerBody);
        AssertInventoryHandlerGuardsDraftBeforeClearingDraft(categoryHandlerBody);
        AssertInventoryHandlerGuardsDraftBeforeClearingDraft(itemHandlerBody);
        Assert.Contains("TrySelectExistingInventoryEntry();", categoryHandlerBody, StringComparison.Ordinal);
        Assert.Contains("TrySelectExistingInventoryEntry();", itemHandlerBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.RememberCategoryItem(selectedItem.CategoryId, selectedItem.ItemId);", itemHandlerBody, StringComparison.Ordinal);
        AssertHandlerRefreshesShellState(content, "EquipmentCharacterComboBox_SelectionChanged", "RefreshEquipmentState();");
        Assert.Contains("TrackEquipmentDraftSelection(sender as ComboBox);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowPersonaHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string memberHandlerBody = GetSection(
            content,
            "private void PersonaMemberComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void PersonaSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string addPersonaBody = GetSection(
            content,
            "private void AddPersonaEdit(List<SaveEditCommand> edits, List<SaveDiagnostic> diagnostics)",
            "private static bool TryValidateCompendiumPersonaExperienceChange(");
        string captureCompendiumDraftBody = GetSection(
            content,
            "private CompendiumDraftState? CaptureSelectedCompendiumDraft()",
            "private void RestoreSelectedCompendiumDraft(CompendiumDraftState compendiumDraft)");
        string restoreCompendiumDraftBody = GetSection(
            content,
            "private void RestoreSelectedCompendiumDraft(CompendiumDraftState compendiumDraft)",
            "internal static bool ShouldRestoreSelectedCompendiumDraft(");
        string personaMemberHandlerBody = GetSection(
            content,
            "private void PersonaMemberComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void PersonaSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string personaSlotHandlerBody = GetSection(
            content,
            "private void PersonaSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void PersonaChoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string compendiumListHandlerBody = GetSection(
            content,
            "private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string compendiumAddHandlerBody = GetSection(
            content,
            "private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)");
        string restoreBlockedDraftBody = GetSection(
            content,
            "private void RestorePersonaSelectionAfterBlockedDraft()",
            "private void InventoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)");

        Assert.Contains("PersonaMemberComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("PersonaSlotComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("PersonaChoiceComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("PersonaSkillBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", content, StringComparison.Ordinal);
        Assert.Contains("TryBuildEditBatch(", content, StringComparison.Ordinal);
        Assert.Contains("SetProtagonistPersonaSlotEdit", content, StringComparison.Ordinal);
        Assert.Contains("SetPartyPersonaSlotEdit", content, StringComparison.Ordinal);
        Assert.Contains("SetCompendiumPersonaSlotEdit", content, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex.HasValue", content, StringComparison.Ordinal);
        Assert.Contains("return TryBuildPersonaSlotEditCore(", content, StringComparison.Ordinal);
        Assert.Contains("int partyPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1, 0, personaSlots.Count - 1);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedPersonaSlotIndex = Math.Clamp(selectedMember.MemberId - 1", content, StringComparison.Ordinal);
        Assert.Contains("ClearSelectedCompendiumContext(ref selectedCompendiumSlotIndex);", memberHandlerBody, StringComparison.Ordinal);
        Assert.Contains("suppressCompendiumEvents = true;", memberHandlerBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView.SelectedItem = null;", memberHandlerBody, StringComparison.Ordinal);
        Assert.Contains("suppressCompendiumEvents = false;", memberHandlerBody, StringComparison.Ordinal);
        Assert.Contains("PreserveCompendiumPersonaIdentity(currentCompendiumSlot, compendiumPersonaSlotEdit)", addPersonaBody, StringComparison.Ordinal);
        Assert.Contains("ushort selectedPersonaId = viewModel.CompendiumPersonaSlots[selectedCompendiumSlotIndex.Value].PersonaId;", captureCompendiumDraftBody, StringComparison.Ordinal);
        Assert.DoesNotContain("PersonaChoiceComboBox.ItemsSource = SaveEditorViewModel.GetPersonaChoices", restoreCompendiumDraftBody, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(content, Regex.Escape("PersonaChoiceComboBox.ItemsSource =")));
        Assert.Contains("PersonaChoiceComboBox.IsEnabled = canEdit && !selectedCompendiumSlotIndex.HasValue;", content, StringComparison.Ordinal);
        AssertPersonaSelectionHandlerGuardsDraftBeforeRefresh(personaMemberHandlerBody);
        AssertPersonaSelectionHandlerGuardsDraftBeforeRefresh(personaSlotHandlerBody);
        AssertPersonaSelectionHandlerGuardsDraftBeforeRefresh(compendiumListHandlerBody);
        AssertPersonaSelectionHandlerGuardsDraftBeforeRefresh(compendiumAddHandlerBody);
        Assert.Contains("viewModel.ProtagonistPersonaSlots[selectedPersonaSlotIndex]", addPersonaBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSocialLinkHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string selectionBody = GetSection(
            content,
            "private void SocialLinkListView_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void SocialLinkAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string addBody = GetSection(
            content,
            "private void SocialLinkAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private bool TryGuardSelectedSocialLinkDraftBeforeOperation()");
        string autoApplyBody = GetSection(
            content,
            "private bool TryGuardSelectedSocialLinkDraftBeforeOperation()",
            "private void RestoreSocialLinkListSelection()");
        string deleteBody = GetSection(
            content,
            "private async void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e)",
            "private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e)");

        Assert.Contains("RefreshSocialLinksState();", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkListView_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkAddComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinkApplyButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDeleteButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("if (!TryGuardSelectedSocialLinkDraftBeforeOperation())", selectionBody, StringComparison.Ordinal);
        Assert.Contains("RestoreSocialLinkListSelection();", selectionBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryGuardSelectedSocialLinkDraftBeforeOperation())", addBody, StringComparison.Ordinal);
        Assert.Contains("ResetSocialLinkAddChoice();", addBody, StringComparison.Ordinal);
        Assert.Contains("TrackEditorDraft();", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSocialLinksState(allowFallbackSelection: selectedSocialLinkIndex.HasValue);", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedSocialLinkIndex = selectedLink.SlotIndex;", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedSocialLinkLinkId = selectedLink.LinkId;", addBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryGuardSelectedSocialLinkDraftBeforeOperation())", deleteBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryAppendSelectedSocialLinkEdits(edits, validationDiagnostics))", autoApplyBody, StringComparison.Ordinal);
        Assert.Contains("SetUiDiagnostics(validationDiagnostics);", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("viewModel.ApplyEdits(edits)", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkLevel(", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkProgress(", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkFlag(", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", addBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", deleteBody, StringComparison.Ordinal);
        Assert.Contains("AddSocialLinkEdit", content, StringComparison.Ordinal);
        Assert.DoesNotContain("viewModel.AddSocialLink", addBody, StringComparison.Ordinal);
        Assert.Contains("RemoveSocialLink(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSocialLinkDraftPreservingSelection(", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSocialLinkDraftPreservingSelection(", deleteBody, StringComparison.Ordinal);
        Assert.DoesNotContain("viewModel.SocialLinks.Any(link => link.LinkId == selectedChoice.LinkId)", addBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowCompendiumHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string selectionBody = GetSection(
            content,
            "private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void CompendiumListView_Tapped(object sender, TappedRoutedEventArgs e)");
        string tappedBody = GetSection(
            content,
            "private void CompendiumListView_Tapped(object sender, TappedRoutedEventArgs e)",
            "private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string addBody = GetSection(
            content,
            "private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)");
        string removeBody = GetSection(
            content,
            "private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");
        string clearBody = GetSection(
            content,
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)",
            "private void RefreshEquipmentState()");
        string helperBody = GetSection(
            content,
            "internal static SaveEditorOperationResult RefreshCompendiumDraftPreservingSelection(",
            "private void RefreshEditableFields()");
        string restoreBlockedDraftBody = GetSection(
            content,
            "private void RestorePersonaSelectionAfterBlockedDraft()",
            "private void InventoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)");

        Assert.Contains("if (CompendiumListView.SelectedItem is not CompendiumPersonaViewState selectedEntry)", selectionBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = null;", selectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedPersonaSlotIndex = 0;", selectionBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", selectionBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", selectionBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = selectedEntry.SlotIndex;", selectionBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumListSlotIndex = selectedEntry.SlotIndex;", selectionBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("Tapped=\"CompendiumListView_Tapped\"", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml")), StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex == selectedCompendiumListSlotIndex", tappedBody, StringComparison.Ordinal);
        Assert.Contains("!IsSelectedCompendiumListItemTapped(e, selectedEntry)", tappedBody, StringComparison.Ordinal);
        Assert.Contains("current is ListViewItem item", tappedBody, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(item.DataContext, selectedEntry)", tappedBody, StringComparison.Ordinal);
        Assert.Contains("current = VisualTreeHelper.GetParent(current);", tappedBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = selectedEntry.SlotIndex;", tappedBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumAddComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView.SelectedItem = selectedCompendiumListSlotIndex.HasValue", restoreBlockedDraftBody, StringComparison.Ordinal);
        Assert.DoesNotContain("CompendiumListView.SelectedItem = null;", restoreBlockedDraftBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumRemoveButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("CompendiumClearButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("RefreshCompendiumState();", content, StringComparison.Ordinal);
        Assert.Contains("selectedChoice.PersonaId == 0", addBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView.SelectedItem = null;", addBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", addBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", addBody, StringComparison.Ordinal);
        Assert.Contains("RefreshCompendiumDraftPreservingSelection(", removeBody, StringComparison.Ordinal);
        Assert.Contains("RefreshCompendiumDraftPreservingSelection(", clearBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryGuardSelectedPersonaDraftBeforeOperation())", removeBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryGuardSelectedPersonaDraftBeforeOperation())", clearBody, StringComparison.Ordinal);
        Assert.True(removeBody.IndexOf("if (!TryGuardSelectedPersonaDraftBeforeOperation())", StringComparison.Ordinal) < removeBody.IndexOf("uiDiagnosticsOverride = null;", StringComparison.Ordinal));
        Assert.True(clearBody.IndexOf("if (!TryGuardSelectedPersonaDraftBeforeOperation())", StringComparison.Ordinal) < clearBody.IndexOf("uiDiagnosticsOverride = null;", StringComparison.Ordinal));
        Assert.Contains("if (result.Succeeded)", helperBody, StringComparison.Ordinal);
        Assert.Contains("clearSelectedCompendiumSlotIndex();", helperBody, StringComparison.Ordinal);
        Assert.Contains("refreshFromViewModelPreservingInventoryQuantityDraft(!result.Succeeded);", helperBody, StringComparison.Ordinal);
        Assert.Contains("TryResolveCompendiumPersonaAddTarget(", content, StringComparison.Ordinal);
        Assert.Contains("SetCompendiumPersonaSlotEdit", content, StringComparison.Ordinal);
        Assert.Contains("TrackEditorDraft();", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectOrAddCompendiumPersona(", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectOrAddCompendiumPersonaCore(", addBody, StringComparison.Ordinal);
        Assert.Contains("ResolveSelectedPersonaSlotIndexForProtagonistView(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("int? selectedCompendiumSlotIndexBeforeMutation = selectedCompendiumSlotIndex;", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModelPreservingInventoryQuantityDraft(preserveSelectedCompendiumDraft: true);", addBody, StringComparison.Ordinal);
        Assert.Contains("ClearCompendiumPersonaSlot(", content, StringComparison.Ordinal);
        Assert.Contains("ClearCompendiumPersonaSlots(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("PersonaId - 1", addBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceDisablesInventoryQuantityWhenNoItemIsSelected()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string updateShellStateBody = GetSection(
            content,
            "private void UpdateShellState()",
            "private void RefreshSocialStatsState()");

        Assert.Contains("inventoryWorkspacePage?.SetInventoryEnabled(canEdit, selectedInventoryItemId.HasValue, selectedInventoryEntryId.HasValue);", updateShellStateBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowCompendiumNoSaveHandlersReturnP4GWINUI025BeforeMutatingState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string removeBody = GetSection(
            content,
            "private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");
        string clearBody = GetSection(
            content,
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)",
            "private void RefreshEquipmentState()");

        string noSaveDiagnostic = "SetUiDiagnostics([CreateUiDiagnostic(\"P4GWINUI025\", \"Open a save before editing the compendium.\", \"Compendium\")]);";
        Assert.Contains("if (!viewModel.HasSave)", removeBody, StringComparison.Ordinal);
        Assert.Contains("if (!viewModel.HasSave)", clearBody, StringComparison.Ordinal);
        Assert.Contains(noSaveDiagnostic, removeBody, StringComparison.Ordinal);
        Assert.Contains(noSaveDiagnostic, clearBody, StringComparison.Ordinal);
        Assert.True(removeBody.IndexOf(noSaveDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("uiDiagnosticsOverride = null;", StringComparison.Ordinal));
        Assert.True(clearBody.IndexOf(noSaveDiagnostic, StringComparison.Ordinal) < clearBody.IndexOf("uiDiagnosticsOverride = null;", StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindowCompendiumRemoveNoSelectionReturnsP4GWINUI026BeforeMutationOrRefresh()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string removeBody = GetSection(
            content,
            "private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");

        string noSelectionDiagnostic = "SetUiDiagnostics([CreateUiDiagnostic(\"P4GWINUI026\", \"Select a compendium entry before removing it.\", \"Compendium.Item\")]);";
        Assert.Contains("if (!selectedCompendiumListSlotIndex.HasValue)", removeBody, StringComparison.Ordinal);
        Assert.Contains(noSelectionDiagnostic, removeBody, StringComparison.Ordinal);
        Assert.True(removeBody.IndexOf(noSelectionDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("uiDiagnosticsOverride = null;", StringComparison.Ordinal));
        Assert.True(removeBody.IndexOf(noSelectionDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("RefreshCompendiumDraftPreservingSelection(", StringComparison.Ordinal));
        Assert.True(removeBody.IndexOf(noSelectionDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindowDestructiveActionsRequireConfirmationBeforeMutation()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string saveBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)",
            "private async Task<string?> PickSavePathAsync()");
        string socialLinkDeleteBody = GetSection(
            content,
            "private async void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e)",
            "private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string compendiumRemoveBody = GetSection(
            content,
            "private async void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");
        string compendiumClearBody = GetSection(
            content,
            "private async void CompendiumClearButton_Click(object sender, RoutedEventArgs e)",
            "private void RefreshEquipmentState()");
        string inventoryDeleteBody = GetSection(
            content,
            "private async void InventoryDeleteButton_Click(object sender, RoutedEventArgs e)",
            "private void PersonaMemberComboBox_SelectionChanged(");
        string confirmationBody = GetSection(
            content,
            "private async Task<bool> ShowConfirmationAsync(string title, string message, string primaryButtonText)",
            "private async Task ReportOpenFailureAsync(string source, string message)");

        Assert.Contains("if (!await ConfirmOverwriteSaveAsync(targetPath))", saveBody, StringComparison.Ordinal);
        Assert.True(saveBody.IndexOf("if (!await ConfirmOverwriteSaveAsync(targetPath))", StringComparison.Ordinal) < saveBody.IndexOf("if (HasPendingEditorDrafts())", StringComparison.Ordinal));

        Assert.Contains("await ShowConfirmationAsync(", socialLinkDeleteBody, StringComparison.Ordinal);
        Assert.Contains("await ShowConfirmationAsync(", compendiumRemoveBody, StringComparison.Ordinal);
        Assert.Contains("await ShowConfirmationAsync(", compendiumClearBody, StringComparison.Ordinal);
        Assert.Contains("await ShowConfirmationAsync(", inventoryDeleteBody, StringComparison.Ordinal);
        Assert.True(socialLinkDeleteBody.IndexOf("await ShowConfirmationAsync(", StringComparison.Ordinal) < socialLinkDeleteBody.IndexOf("viewModel.RemoveSocialLink", StringComparison.Ordinal));
        Assert.True(compendiumRemoveBody.IndexOf("await ShowConfirmationAsync(", StringComparison.Ordinal) < compendiumRemoveBody.IndexOf("viewModel.ClearCompendiumPersonaSlot", StringComparison.Ordinal));
        Assert.True(compendiumClearBody.IndexOf("await ShowConfirmationAsync(", StringComparison.Ordinal) < compendiumClearBody.IndexOf("viewModel.ClearCompendiumPersonaSlots", StringComparison.Ordinal));
        Assert.True(inventoryDeleteBody.IndexOf("await ShowConfirmationAsync(", StringComparison.Ordinal) < inventoryDeleteBody.IndexOf("viewModel.RemoveInventoryItem", StringComparison.Ordinal));
        Assert.Contains("PrimaryButtonText = primaryButtonText", confirmationBody, StringComparison.Ordinal);
        Assert.Contains("CloseButtonText = \"Cancel\"", confirmationBody, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Close", confirmationBody, StringComparison.Ordinal);
        Assert.Contains("ContentDialogResult.Primary", confirmationBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowAutoSelectsFirstCompendiumEntryOnOpen()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string openBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> OpenSaveFileAsync()",
            "private bool ApplyEditorFields()");
        string refreshCompendiumBody = GetSection(
            content,
            "private void RefreshCompendiumState()",
            "private void RefreshInventoryState()");

        Assert.Contains("autoSelectCompendiumEntryAfterOpen = true;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumListSlotIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("ResolveSelectedCompendiumViewState(", refreshCompendiumBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectCompendiumEntryAfterOpen", refreshCompendiumBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumListSlotIndex = selectedEntry.SlotIndex;", refreshCompendiumBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedCompendiumSlotIndex = selectedEntry.SlotIndex;", refreshCompendiumBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectCompendiumEntryAfterOpen = false;", refreshCompendiumBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowLaunchOpenPathReportsPersistenceFailures()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string openBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> OpenSaveFileFromPathAsync(string path, string source)",
            "private async void MainWindow_Loaded(object sender, RoutedEventArgs e)");
        string helperBody = GetSection(
            content,
            "private static bool IsPersistenceException(Exception ex) =>",
            "private static string FormatPersonaSlot(PersonaSlotViewState slot) =>");

        Assert.Contains("catch (Exception ex) when (IsPersistenceException(ex))", openBody, StringComparison.Ordinal);
        Assert.Contains("await ReportOpenFailureAsync(source, $\"Could not read the selected file: {ex.Message}\");", openBody, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", openBody, StringComparison.Ordinal);
        Assert.Contains("await ReportOpenFailureAsync(source, $\"Could not open the selected file: {ex.Message}\");", openBody, StringComparison.Ordinal);
        Assert.Contains("ArgumentException", helperBody, StringComparison.Ordinal);
        Assert.Contains("NotSupportedException", helperBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowLoadedUsesBusyRefreshForStartupOpen()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string loadedBody = GetSection(
            content,
            "private async void MainWindow_Loaded(object sender, RoutedEventArgs e)",
            "private async void MainWindow_DragOver(object sender, DragEventArgs e)");

        Assert.Contains("if (string.IsNullOrWhiteSpace(startupOpenPath))", loadedBody, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(RefreshBasicFieldsFromViewModel);", loadedBody, StringComparison.Ordinal);
        Assert.Contains("await RunBusyAsync(", loadedBody, StringComparison.Ordinal);
        Assert.Contains("ConsumeStartupOpenPath(ref this.startupOpenPath)", loadedBody, StringComparison.Ordinal);
        Assert.Contains("OpenSaveFileFromPathAsync(startupOpenPath, \"Launch\")", loadedBody, StringComparison.Ordinal);
        Assert.Contains("if (refreshEditableFieldsAfterStartupOpen)", loadedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = DispatcherQueue.TryEnqueue(() =>", loadedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("await Task.Delay(5_000);", loadedBody, StringComparison.Ordinal);
        Assert.Contains("RefreshEditableFields();", loadedBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRefreshFromViewModelUsesFullRefreshOutsideStartupOpen()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string refreshBody = GetSection(
            content,
            "private void RefreshFromViewModel()",
            "private void RefreshFromViewModelPreservingInventoryQuantityDraft(");

        Assert.Contains("if (refreshEditableFieldsAfterStartupOpen)", refreshBody, StringComparison.Ordinal);
        Assert.Contains("RefreshEditableFields();", refreshBody, StringComparison.Ordinal);
        Assert.Contains("RefreshBasicFieldsFromViewModel();", refreshBody, StringComparison.Ordinal);
        Assert.DoesNotContain("await Task.Delay(5_000);", refreshBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowStartupRefreshKeepsEditingDisabledUntilEditableFieldsRefreshCompletes()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string refreshBody = GetSection(
            content,
            "private void RefreshEditableFields()",
            "private void RefreshBasicStatsState()");
        string updateShellStateBody = GetSection(
            content,
            "private void UpdateShellState()",
            "private void RefreshSocialStatsState()");

        Assert.Contains("DispatcherQueue.TryEnqueue(RefreshRemainingEditableFields)", refreshBody, StringComparison.Ordinal);
        Assert.DoesNotContain("await Task.Delay(5_000);", refreshBody, StringComparison.Ordinal);
        Assert.Contains("refreshEditableFieldsAfterStartupOpen = false;", refreshBody, StringComparison.Ordinal);
        Assert.Contains("bool startupRefreshPending = refreshEditableFieldsAfterStartupOpen;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("!startupRefreshPending", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("bool hasPendingEditorDrafts = HasPendingEditorDrafts();", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("bool canApply = canEdit && hasPendingEditorDrafts;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("bool canSave = canEdit && viewModel.IsDirty && viewModel.CanWrite && !hasPendingEditorDrafts;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("ApplyButton.IsEnabled = canApply;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("bool canSaveAs = canSave;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("FileOpenMenuItem.IsEnabled = !isBusy && !startupRefreshPending;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("OpenButton.IsEnabled = !isBusy && !startupRefreshPending;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SaveAsButton.IsEnabled = canSaveAs;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("FileSaveAsMenuItem.IsEnabled = canSaveAs;", updateShellStateBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceBuildsMainCharacterEditsAndUsesExperienceHelper()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string editBatchBody = GetSection(
            content,
            "private bool TryBuildEditBatch(",
            "private static void AddPartyMemberValue(");
        string refreshBasicStatsBody = GetSection(
            content,
            "private void RefreshBasicStatsState()",
            "private void UpdateShellState()");

        Assert.Contains("new SetMainCharacterLevelEdit((byte)GetCurrentMainCharacterLevelRawValue())", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("new SetMainCharacterTotalExperienceEdit(parsedMainCharacterTotalExperience)", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("P4GWINUI028", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("LevelExperienceProjection.CalculateTotalExperienceFromLevel", content, StringComparison.Ordinal);
        Assert.Contains("basicStatsWorkspacePage.SetMainCharacterLevelRawValue(viewModel.HasSave ? viewModel.MainCharacterLevel : 0);", refreshBasicStatsBody, StringComparison.Ordinal);
        Assert.Contains("UpdateMainCharacterLevelValueText();", refreshBasicStatsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Max(1d, viewModel.MainCharacterLevel)", refreshBasicStatsBody, StringComparison.Ordinal);
        Assert.Contains("SectionNavigationView.SelectedItem = JumpOverviewButton;", content, StringComparison.Ordinal);
        Assert.Contains("private void SectionNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToSelectedSection(sectionTag);", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToBasicStatsWorkspace();", content, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToSection(BasicStatsSectionHeader);", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToCalendarSocialStatsWorkspace();", content, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToSection(CalendarSocialStatsSectionHeader);", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToOverviewWorkspace();", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToDiagnosticsWorkspace();", content, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToSection(DiagnosticsStateSectionHeader);", content, StringComparison.Ordinal);
        Assert.Contains("target.StartBringIntoView();", content, StringComparison.Ordinal);
        Assert.Contains("PersonaCalculateFromLevelButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("PersonaLevelSlider_ValueChanged", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowInvalidImmediateInputBranchesRefreshShellState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string yenBody = GetSection(
            content,
            "private void YenTextBox_TextChanged(object sender, TextChangedEventArgs e)",
            "private void MainCharacterTotalExperienceTextBox_TextChanged(object sender, TextChangedEventArgs e)");
        string mainCharacterExperienceBody = GetSection(
            content,
            "private void MainCharacterTotalExperienceTextBox_TextChanged(object sender, TextChangedEventArgs e)",
            "private void CalendarSocialStatsWorkspacePage_SocialStatSelectionChanged(");
        string dayBody = GetSection(
            content,
            "private void TrackDayDraftEdit(string text, bool isNextDay)",
            "private void CalendarSocialStatsWorkspacePage_PhaseSelectionChanged(");
        string socialLinkTextBody = GetSection(
            content,
            "private void SocialLinkTextBox_TextChanged(object sender, TextChangedEventArgs e)",
            "private bool CanProcessImmediateEditEvent()");

        AssertDiagnosticBranchRefreshesShellState(yenBody, "P4GWINUI006");
        AssertDiagnosticBranchRefreshesShellState(mainCharacterExperienceBody, "P4GWINUI028");
        AssertDiagnosticBranchRefreshesShellState(dayBody, "P4GWINUI018");
        AssertDiagnosticBranchRefreshesShellState(dayBody, "P4GWINUI020");
        Assert.Contains("SetUiDiagnostics(diagnostics);", socialLinkTextBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", socialLinkTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialLinksWorkspacePageDeclaresSocialLinkEditingControls()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "Workspaces", "SocialLinksWorkspacePage.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"SocialLinkListView\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"SocialLinkListView_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkAddComboBox\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"SocialLinkAddComboBox_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkLevelTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkProgressTextBox\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"SocialLinkFlagTextBox\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"SocialLinkApplyButton\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"SocialLinkApplyButton_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinkDeleteButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"SocialLinkDeleteButton_Click\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlDeclaresSectionNavigationAndHelperControls()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string personaLevelSlider = GetSection(
            content,
            "x:Name=\"PersonaLevelSlider\"",
            "x:Name=\"PersonaLevelValueTextBlock\"");

        Assert.Contains("<NavigationView", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SectionNavigationView\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"SectionNavigationView_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationView.MenuItems>", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpOverviewButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Content=\"Overview\"", content, StringComparison.Ordinal);
        Assert.Contains("Icon=\"Home\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Overview\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpBasicStatsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"BasicStats\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpCalendarSocialStatsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"CalendarSocialStats\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpSocialLinksButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"SocialLinks\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpPartyPersonaButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"PartyPersona\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpEquipmentButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Equipment\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpCompendiumButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Compendium\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpInventoryButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"Inventory\"", content, StringComparison.Ordinal);
        Assert.Contains("<NavigationViewItem x:Name=\"JumpDiagnosticsStateButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Tag=\"DiagnosticsState\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button x:Name=\"Jump", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BasicStatsSectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"CalendarSocialStatsSectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"SocialLinksSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PartyPersonaSectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"EquipmentSectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DiagnosticsStateSectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DiagnosticsListView\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompendiumSectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"InventorySectionHeader\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"FamilyNameTextBox\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"GivenNameTextBox\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"YenTextBox\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterLevelSlider\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterLevelValueTextBlock\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterTotalExperienceTextBox\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterCalculateFromLevelButton\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PersonaCalculateFromLevelButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Minimum=\"0\"", personaLevelSlider, StringComparison.Ordinal);
        Assert.Contains("Click=\"PersonaCalculateFromLevelButton_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PersonaLevelValueTextBlock\"", content, StringComparison.Ordinal);
        Assert.Contains("ValueChanged=\"PersonaLevelSlider_ValueChanged\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=MainCharacterLevelSlider", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=PersonaLevelSlider", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"SectionNavigationView\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"JumpOverviewButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Overview\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"JumpBasicStatsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("VerticalScrollBarVisibility=\"Disabled\"", content, StringComparison.Ordinal);
        Assert.True(
            content.IndexOf("x:Name=\"JumpOverviewButton\"", StringComparison.Ordinal) <
            content.IndexOf("x:Name=\"JumpBasicStatsButton\"", StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindowRoutesWorkspacesThroughFrameHostPage()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string selectionHandlerBody = GetSection(
            source,
            "private void SectionNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)",
            "private void NavigateToWorkspace(string sectionTag)");
        string routeBody = GetSection(
            source,
            "private void EnsureLegacyWorkspaceRouted()",
            "private void WorkspaceFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)");
        string selectedSectionBody = GetSection(
            source,
            "private void NavigateToSelectedSection(string sectionTag)",
            "private void JumpSocialLinks_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("<Frame", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkspaceFrame\"", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationFailed=\"WorkspaceFrame_NavigationFailed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LegacyWorkspaceContentStore\"", xaml, StringComparison.Ordinal);
        Assert.True(
            xaml.IndexOf("x:Name=\"WorkspaceFrame\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"LegacyWorkspaceContentStore\"", StringComparison.Ordinal));
        Assert.Contains("NavigateToWorkspace(sectionTag);", selectionHandlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToSelectedSection(sectionTag);", selectionHandlerBody, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(WorkspaceHostPage), LegacyWorkspaceContentStore)", routeBody, StringComparison.Ordinal);
        Assert.Contains("hostPage.SetWorkspaceContent(LegacyWorkspaceContentStore);", routeBody, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.BackStack.Clear();", routeBody, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"Overview\")", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(OverviewWorkspacePage), CreateOverviewWorkspaceState())", source, StringComparison.Ordinal);
        Assert.Contains("overviewPage.SetOverviewState(overviewState);", source, StringComparison.Ordinal);
        Assert.Contains("overviewPage.OpenSaveRequested -= OverviewWorkspacePage_OpenSaveRequested;", source, StringComparison.Ordinal);
        Assert.Contains("overviewPage.OpenSaveRequested += OverviewWorkspacePage_OpenSaveRequested;", source, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"DiagnosticsState\")", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(DiagnosticsWorkspacePage), diagnosticsItems)", source, StringComparison.Ordinal);
        Assert.Contains("diagnosticsPage.SetDiagnosticsItems(diagnosticsItems);", source, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"BasicStats\")", source, StringComparison.Ordinal);
        Assert.Contains("NavigateToBasicStatsWorkspace();", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(BasicStatsWorkspacePage))", source, StringComparison.Ordinal);
        Assert.Contains("ConfigureBasicStatsWorkspacePage(navigatedPage);", source, StringComparison.Ordinal);
        Assert.Contains("basicStatsWorkspacePage = page;", source, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"CalendarSocialStats\")", source, StringComparison.Ordinal);
        Assert.Contains("NavigateToCalendarSocialStatsWorkspace();", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(CalendarSocialStatsWorkspacePage))", source, StringComparison.Ordinal);
        Assert.Contains("ConfigureCalendarSocialStatsWorkspacePage(navigatedPage);", source, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage = page;", source, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"SocialLinks\")", source, StringComparison.Ordinal);
        Assert.Contains("NavigateToSocialLinksWorkspace();", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(SocialLinksWorkspacePage))", source, StringComparison.Ordinal);
        Assert.Contains("ConfigureSocialLinksWorkspacePage(navigatedPage);", source, StringComparison.Ordinal);
        Assert.Contains("socialLinksWorkspacePage = page;", source, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"Equipment\")", source, StringComparison.Ordinal);
        Assert.Contains("NavigateToEquipmentWorkspace();", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(EquipmentWorkspacePage))", source, StringComparison.Ordinal);
        Assert.Contains("ConfigureEquipmentWorkspacePage(navigatedPage);", source, StringComparison.Ordinal);
        Assert.Contains("equipmentWorkspacePage = page;", source, StringComparison.Ordinal);
        Assert.Contains("if (sectionTag == \"Inventory\")", source, StringComparison.Ordinal);
        Assert.Contains("NavigateToInventoryWorkspace();", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(InventoryWorkspacePage))", source, StringComparison.Ordinal);
        Assert.Contains("ConfigureInventoryWorkspacePage(navigatedPage);", source, StringComparison.Ordinal);
        Assert.Contains("inventoryWorkspacePage = page;", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("if (sectionTag == \"CalendarSocialStats\")", StringComparison.Ordinal) <
            source.IndexOf("EnsureLegacyWorkspaceRouted();", StringComparison.Ordinal));
        Assert.True(
            source.IndexOf("if (sectionTag == \"SocialLinks\")", StringComparison.Ordinal) <
            source.IndexOf("EnsureLegacyWorkspaceRouted();", StringComparison.Ordinal));
        Assert.True(
            source.IndexOf("if (sectionTag == \"Equipment\")", StringComparison.Ordinal) <
            source.IndexOf("EnsureLegacyWorkspaceRouted();", StringComparison.Ordinal));
        Assert.True(
            source.IndexOf("if (sectionTag == \"Inventory\")", StringComparison.Ordinal) <
            source.IndexOf("EnsureLegacyWorkspaceRouted();", StringComparison.Ordinal));
        Assert.DoesNotContain("case \"BasicStats\":", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("BasicStatsSectionHeader", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"CalendarSocialStats\":", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("CalendarSocialStatsSectionHeader", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"SocialLinks\":", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinksSectionHeader", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"Equipment\":", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("EquipmentSectionHeader", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"Inventory\":", selectedSectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("InventorySectionHeader", selectedSectionBody, StringComparison.Ordinal);
        Assert.Equal(8, Regex.Count(source, Regex.Escape("WorkspaceFrame.BackStack.Clear();")));
    }

    [Fact]
    public void WorkspaceHostPageOwnsRoutedContentSlot()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "WorkspaceHostPage.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "WorkspaceHostPage.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Class=\"P4G.SaveTool.WinUI.WorkspaceHostPage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ContentControl x:Name=\"WorkspaceContentHost\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class WorkspaceHostPage : Page", source, StringComparison.Ordinal);
        Assert.Contains("protected override void OnNavigatedTo(NavigationEventArgs e)", source, StringComparison.Ordinal);
        Assert.Contains("e.Parameter is UIElement content", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceContentHost.Content = null;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OverviewWorkspacePageOwnsOverviewAndNoSaveState()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "OverviewWorkspacePage.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "OverviewWorkspacePage.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowXaml = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Class=\"P4G.SaveTool.WinUI.OverviewWorkspacePage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class OverviewWorkspacePage : Page", source, StringComparison.Ordinal);
        Assert.Contains("internal readonly record struct OverviewWorkspaceViewState(", source, StringComparison.Ordinal);
        Assert.Contains("bool HasSave", source, StringComparison.Ordinal);
        Assert.Contains("string FilePathText", source, StringComparison.Ordinal);
        Assert.Contains("string StateText", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewNoSaveEmptyStateBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Open a Persona 4 Golden save", xaml, StringComparison.Ordinal);
        Assert.Contains("Drop a save file here, or choose Open save to begin editing.", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewOpenButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"OverviewOpenButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("event EventHandler<RoutedEventArgs>? OpenSaveRequested", source, StringComparison.Ordinal);
        Assert.Contains("OpenSaveRequested?.Invoke(this, e);", source, StringComparison.Ordinal);
        Assert.Contains("Text=\"Overview\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewFilePathTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewStateTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Choose a workspace", xaml, StringComparison.Ordinal);
        Assert.Contains("Use the navigation pane to edit a specific save area.", xaml, StringComparison.Ordinal);
        Assert.Contains("OverviewNoSaveEmptyStateBorder.Visibility = state.HasSave ? Visibility.Collapsed : Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.Contains("OverviewLoadedContent.Visibility = state.HasSave ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("OverviewFilePathTextBlock.Text = state.FilePathText;", source, StringComparison.Ordinal);
        Assert.Contains("OverviewStateTextBlock.Text = state.StateText;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveEditor", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ComboBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Slider", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"NoSaveEmptyStateBorder\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"NoSaveOpenButton\"", mainWindowXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicStatsWorkspacePageOwnsBasicStatsEditorControls()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "BasicStatsWorkspacePage.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "BasicStatsWorkspacePage.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowXaml = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowSource = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string configureBody = GetSection(
            mainWindowSource,
            "private void ConfigureBasicStatsWorkspacePage(BasicStatsWorkspacePage page)",
            "private void WorkspaceFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)");

        Assert.Contains("x:Class=\"P4G.SaveTool.WinUI.BasicStatsWorkspacePage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class BasicStatsWorkspacePage : Page", source, StringComparison.Ordinal);
        Assert.Contains("NavigationCacheMode = NavigationCacheMode.Required;", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BasicStatsSectionHeader\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Basic / Stats\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FamilyNameTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"FamilyNameTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GivenNameTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"GivenNameTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"YenTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"YenTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterLevelSlider\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Minimum=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"255\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"MainCharacterLevelSlider\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Main character level\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterLevelValueTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterTotalExperienceTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"MainCharacterTotalExperienceTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterCalculateFromLevelButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"MainCharacterCalculateFromLevelButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("event TextChangedEventHandler? FamilyNameTextChanged", source, StringComparison.Ordinal);
        Assert.Contains("event TextChangedEventHandler? GivenNameTextChanged", source, StringComparison.Ordinal);
        Assert.Contains("event TextChangedEventHandler? YenTextChanged", source, StringComparison.Ordinal);
        Assert.Contains("event RangeBaseValueChangedEventHandler? MainCharacterLevelValueChanged", source, StringComparison.Ordinal);
        Assert.Contains("event TextChangedEventHandler? MainCharacterTotalExperienceTextChanged", source, StringComparison.Ordinal);
        Assert.Contains("event RoutedEventHandler? MainCharacterCalculateFromLevelClick", source, StringComparison.Ordinal);
        Assert.Contains("internal string FamilyNameText", source, StringComparison.Ordinal);
        Assert.Contains("internal double MainCharacterLevelRawValue", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetBasicStatsEnabled(bool isEnabled)", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetMainCharacterLevelRawValue(double rawLevel)", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetMainCharacterLevelValueText(string text)", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetMainCharacterLevelValueForeground(Brush? foreground)", source, StringComparison.Ordinal);
        Assert.Contains("page.FamilyNameTextChanged -= FamilyNameTextBox_TextChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.FamilyNameTextChanged += FamilyNameTextBox_TextChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.MainCharacterLevelValueChanged -= MainCharacterLevelSlider_ValueChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.MainCharacterLevelValueChanged += MainCharacterLevelSlider_ValueChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.MainCharacterCalculateFromLevelClick -= MainCharacterCalculateFromLevelButton_Click;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.MainCharacterCalculateFromLevelClick += MainCharacterCalculateFromLevelButton_Click;", configureBody, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BasicStatsSectionHeader\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"FamilyNameTextBox\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"GivenNameTextBox\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"YenTextBox\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterLevelSlider\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterTotalExperienceTextBox\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"MainCharacterCalculateFromLevelButton\"", mainWindowXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarSocialStatsWorkspacePageOwnsCalendarSocialStatsEditorControls()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "CalendarSocialStatsWorkspacePage.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "CalendarSocialStatsWorkspacePage.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowXaml = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowSource = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string configureBody = GetSection(
            mainWindowSource,
            "private void ConfigureCalendarSocialStatsWorkspacePage(CalendarSocialStatsWorkspacePage page)",
            "private void WorkspaceFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)");

        Assert.Contains("x:Class=\"P4G.SaveTool.WinUI.CalendarSocialStatsWorkspacePage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class CalendarSocialStatsWorkspacePage : Page", source, StringComparison.Ordinal);
        Assert.Contains("NavigationCacheMode = NavigationCacheMode.Required;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{x:Bind", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CalendarSocialStatsSectionHeader\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Calendar / Social Stats\"", xaml, StringComparison.Ordinal);

        string[] interactiveControlNames =
        [
            "CourageComboBox",
            "KnowledgeComboBox",
            "ExpressionComboBox",
            "UnderstandingComboBox",
            "DiligenceComboBox",
            "DayTextBox",
            "PhaseComboBox",
            "NextDayTextBox",
            "NextPhaseComboBox",
        ];

        foreach (string controlName in interactiveControlNames)
        {
            Assert.Contains($"x:Name=\"{controlName}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"automation:AutomationProperties.AutomationId=\"{controlName}\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain($"x:Name=\"{controlName}\"", mainWindowXaml, StringComparison.Ordinal);
        }

        Assert.Contains("automation:AutomationProperties.Name=\"Courage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Knowledge\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Expression\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Understanding\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Diligence\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Day\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Phase\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Next day\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Next phase\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ComboBox.ItemTemplate><DataTemplate><TextBlock Text=\"{Binding}\" /></DataTemplate></ComboBox.ItemTemplate>", xaml, StringComparison.Ordinal);

        Assert.Contains("event EventHandler<SocialStatSelectionChangedEventArgs>? SocialStatSelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("event EventHandler<CalendarDayTextChangedEventArgs>? DayTextChanged", source, StringComparison.Ordinal);
        Assert.Contains("event EventHandler<CalendarPhaseSelectionChangedEventArgs>? PhaseSelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("internal SocialStatRankChoiceViewState? GetSocialStatSelectedRank(int statIndex)", source, StringComparison.Ordinal);
        Assert.Contains("internal CalendarPhaseChoiceViewState? GetCalendarPhaseSelectedChoice(bool isNextPhase)", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetCalendarSocialStatsEnabled(bool isEnabled)", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetSocialStatSelection(", source, StringComparison.Ordinal);
        Assert.Contains("internal void ClearSocialStatChoices(int statIndex)", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetCalendarPhaseSelection(", source, StringComparison.Ordinal);
        Assert.Contains("internal void ClearCalendarPhaseChoices(bool isNextPhase)", source, StringComparison.Ordinal);
        Assert.Contains("page.SocialStatSelectionChanged += CalendarSocialStatsWorkspacePage_SocialStatSelectionChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.DayTextChanged += CalendarSocialStatsWorkspacePage_DayTextChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.PhaseSelectionChanged += CalendarSocialStatsWorkspacePage_PhaseSelectionChanged;", configureBody, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"CalendarSocialStatsSectionHeader\"", mainWindowXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsWorkspacePageOwnsDiagnosticsList()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "DiagnosticsWorkspacePage.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "DiagnosticsWorkspacePage.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowXaml = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowSource = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Class=\"P4G.SaveTool.WinUI.DiagnosticsWorkspacePage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Diagnostics / State\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{ThemeResource SubtitleTextBlockStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NoDiagnosticsTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"NoDiagnosticsTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"No diagnostics.\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DiagnosticsListView\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"DiagnosticsListView\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Severity}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Code}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Target}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Message}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{x:Bind", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class DiagnosticsWorkspacePage : Page", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetDiagnosticsItems(object? itemsSource)", source, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsListView.ItemsSource = itemsSource;", source, StringComparison.Ordinal);
        Assert.Contains("diagnosticsCollection.CollectionChanged += DiagnosticsCollection_CollectionChanged;", source, StringComparison.Ordinal);
        Assert.Contains("diagnosticsCollection.CollectionChanged -= DiagnosticsCollection_CollectionChanged;", source, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsListView.Visibility = hasDiagnostics ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("NoDiagnosticsTextBlock.Visibility = hasDiagnostics ? Visibility.Collapsed : Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.Contains("DiagnosticListItemViewState { Code: not \"Status\" }", source, StringComparison.Ordinal);
        Assert.Contains("protected override void OnNavigatedTo(NavigationEventArgs e)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DiagnosticsListView\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DiagnosticsStateSectionHeader\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticsStateSectionHeader", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToSection(DiagnosticsStateSectionHeader)", mainWindowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSetsExplicitMultiPaneWindowSize()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string resizeBody = GetSection(
            content,
            "private void ResizeToDefaultMultiPaneSize()",
            "private async void OpenButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("private const int DefaultWindowWidthDip = 1180;", content, StringComparison.Ordinal);
        Assert.Contains("private const int DefaultWindowHeightDip = 820;", content, StringComparison.Ordinal);
        Assert.Contains("[DllImport(\"user32.dll\")]", content, StringComparison.Ordinal);
        Assert.Contains("private static extern uint GetDpiForWindow(IntPtr hWnd);", content, StringComparison.Ordinal);
        Assert.Contains("ResizeToDefaultMultiPaneSize();", content, StringComparison.Ordinal);
        Assert.Contains("Win32Interop.GetWindowFromWindowId(AppWindow.Id)", resizeBody, StringComparison.Ordinal);
        Assert.Contains("GetDpiForWindow(hwnd) / 96.0", resizeBody, StringComparison.Ordinal);
        Assert.Contains("AppWindow.Resize(new SizeInt32(", resizeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"1180\"", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml")), StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"820\"", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml")), StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlDeclaresCompendiumEditingControls()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"CompendiumListView\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"CompendiumListView_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompendiumAddComboBox\"", content, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"CompendiumAddComboBox_SelectionChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompendiumRemoveButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"CompendiumRemoveButton_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompendiumClearButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"CompendiumClearButton_Click\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlBindsCompendiumListItemsWithoutPropertyReflection()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string compendiumSection = GetSection(
            content,
            "              x:Name=\"CompendiumSectionHeader\"",
            "              x:Name=\"PersonaSummaryTextBox\"");

        Assert.Contains("Text=\"{Binding}\"", compendiumSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding DisplayName}\"", compendiumSection, StringComparison.Ordinal);
        Assert.DoesNotContain("{x:Bind", compendiumSection, StringComparison.Ordinal);
        Assert.Contains("<DataTemplate>", compendiumSection, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlUsesRuntimeBindingsWithNativeAotPreservation()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));
        string[] dataTemplateLines = xaml
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("<DataTemplate", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(dataTemplateLines);
        Assert.Contains("Text=\"{Binding}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding DisplayName}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding Quantity}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{x:Bind", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:DataType=", xaml, StringComparison.Ordinal);
        Assert.Contains("XamlBindingPreservation.PreserveXamlBindingProperties();", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "App.xaml.cs")), StringComparison.Ordinal);
        Assert.Contains("typeof(DiagnosticListItemViewState)", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "XamlBindingPreservation.cs")), StringComparison.Ordinal);
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

        Assert.Contains("socialLinksWorkspacePage?.SetSocialLinksEnabled(canEdit, selectedSocialLinkIndex.HasValue);", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinkFlagTextBox", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinkApplyButton", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinkDeleteButton.IsEnabled = canEdit", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumAddComboBox.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumRemoveButton.IsEnabled = canEdit && selectedCompendiumListSlotIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumClearButton.IsEnabled = canEdit && compendiumItems.Count > 0;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("basicStatsWorkspacePage?.SetBasicStatsEnabled(canEdit);", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("MainCharacterLevelSlider.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("MainCharacterTotalExperienceTextBox.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("MainCharacterCalculateFromLevelButton.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("PersonaCalculateFromLevelButton.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
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
        string compendiumBranchBody = GetSection(
            refreshPersonaStateBody,
            "if (selectedCompendiumSlotIndex.HasValue)",
            "PartyMemberChoiceViewState? selectedMember = null;");

        Assert.Contains("RefreshPersonaSummary();", refreshPersonaStateBody, StringComparison.Ordinal);
        Assert.Contains("selectedPersonaMemberId = null;", memberHandlerBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaSummary();", memberHandlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedPersonaMemberId = null;", compendiumBranchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedPersonaSlotIndex = 0;", compendiumBranchBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowEquipmentSelectionChangesOnlyTrackDrafts()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void TrackEquipmentDraftSelection(ComboBox? comboBox)",
            "private bool TryReadInventoryQuantity(");
        string editBatchBody = GetSection(
            content,
            "private void AddSelectedEquipmentEdits(",
            "private static void AddEquipmentEdit(");
        Assert.Contains("TrackEditorDraft();", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetEquipped", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshEquipmentState();", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModel();", methodBody, StringComparison.Ordinal);
        Assert.Contains("new SetEquippedWeaponEdit", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("new SetEquippedArmorEdit", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("new SetEquippedAccessoryEdit", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("new SetEquippedCostumeEdit", editBatchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("preservePersonaEditorStateDuringEquipmentRefresh", content, StringComparison.Ordinal);
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
            "private async void InventoryDeleteButton_Click(");
        string deleteBody = GetSection(
            content,
            "private async void InventoryDeleteButton_Click(object sender, RoutedEventArgs e)",
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
            "private void RefreshFromViewModelPreservingInventoryQuantityDraft(",
            "private void RefreshEditableFields()");

        Assert.Contains("BusyOperationCompletion completion = BusyOperationCompletion.RefreshViewModel;", runBusyBody, StringComparison.Ordinal);
        Assert.Contains("if (completion == BusyOperationCompletion.RefreshViewModel)", runBusyBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", runBusyBody, StringComparison.Ordinal);
        Assert.Contains("if (string.IsNullOrWhiteSpace(startupOpenPath))", content, StringComparison.Ordinal);
        Assert.Contains("await RunBusyAsync(", content, StringComparison.Ordinal);
        Assert.Contains("ConsumeStartupOpenPath(ref this.startupOpenPath)", content, StringComparison.Ordinal);
        Assert.Contains("RefreshFromViewModel();", content, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.IsFullRefreshSuppressed", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);", propertyChangedBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("ShouldPreserveSelectedSocialLinkDraftAfterApply(edits)", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("ShouldPreserveSelectedCompendiumDraftAfterApply(edits)", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("RefreshFromViewModelPreservingInventoryQuantityDraft(", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModelPreservingInventoryQuantityDraft();", applyEditorFieldsBody, StringComparison.Ordinal);
        Assert.Contains("if (HasPendingEditorDrafts())", saveBody, StringComparison.Ordinal);
        Assert.Contains("P4GWINUI037", saveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyEditorFields()", saveBody, StringComparison.Ordinal);
        Assert.True(
            saveBody.IndexOf("targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)", StringComparison.Ordinal) <
            saveBody.IndexOf("if (HasPendingEditorDrafts())", StringComparison.Ordinal));
        Assert.DoesNotContain("CreateBlankSave", saveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreNoSaveStateAfterFailedBlankSave", saveBody, StringComparison.Ordinal);
        Assert.Contains("if (string.IsNullOrWhiteSpace(targetPath))", saveBody, StringComparison.Ordinal);
        Assert.Contains("RefreshFromViewModelPreservingInventoryQuantityDraft();", saveBody, StringComparison.Ordinal);
        Assert.Contains("currentFilePath = targetPath;", saveBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", saveBody, StringComparison.Ordinal);
        Assert.Contains("return BusyOperationCompletion.PreserveEditorState;", saveBody, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(saveBody, Regex.Escape("RefreshFromViewModelPreservingInventoryQuantityDraft();")));
        Assert.Contains("string inventoryQuantityDraft = inventoryWorkspacePage?.QuantityText ?? string.Empty;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDraftState? socialLinkDraft = CaptureSelectedSocialLinkDraft();", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumDraftState? compendiumDraft = preserveSelectedCompendiumDraft ? CaptureSelectedCompendiumDraft() : null;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("InventorySelectionState.ShouldRestoreQuantityDraft(", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("inventoryWorkspacePage.QuantityText = inventoryQuantityDraft;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("if (preserveSelectedSocialLinkDraft && socialLinkDraft is not null)", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("RestoreSelectedSocialLinkDraft(socialLinkDraft.Value);", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("if (compendiumDraft is not null)", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("RestoreSelectedCompendiumDraft(compendiumDraft.Value);", inventoryRefreshBody, StringComparison.Ordinal);
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
        Assert.Equal(4, Regex.Count(saveBody, Regex.Escape("RefreshFromViewModelPreservingInventoryQuantityDraft();")));
    }

    [Fact]
    public void MainWindowZeroQuantityInventoryUpdatesSuppressAutoSelectLikeDelete()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void InventoryAddUpdateButton_Click(",
            "private async void InventoryDeleteButton_Click(");

        Assert.Contains("if (quantity == 0)", methodBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowImmediateInventoryQuantityEditOnlyTracksDraft()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void InventoryQuantityTextBox_TextChanged(",
            "private void FamilyNameTextBox_TextChanged(");

        Assert.Contains("inventoryQuantityDraftDirty = true;", methodBody, StringComparison.Ordinal);
        Assert.Contains("TryReadInventoryQuantity(out _)", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetInventoryItemQuantity(", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshInventoryState();", methodBody, StringComparison.Ordinal);
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
    public void MainWindowOpenAndResetClearsPersonaInventoryEquipmentAndSocialLinkState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string openBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> OpenSaveFileAsync()",
            "private bool ApplyEditorFields()");
        string refreshSocialLinksBody = GetSection(
            content,
            "private void RefreshSocialLinksState(bool allowFallbackSelection = false)",
            "private void RefreshInventoryState()");

        Assert.Contains("selectedInventoryCategoryId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedInventoryItemId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedInventoryEntryId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedEquipmentCharacterId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumListSlotIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedPersonaMemberId = 0;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedPersonaSlotIndex = 0;", openBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.Reset();", openBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectInventoryEntryAfterOpen = true;", openBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectCompendiumEntryAfterOpen = true;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedSocialLinkIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedSocialLinkLinkId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("inventoryWorkspacePage.QuantityText = string.Empty;", openBody, StringComparison.Ordinal);
        Assert.Contains("ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);", refreshSocialLinksBody, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryWorkspacePageOwnsInventoryEditorSurface()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "InventoryWorkspacePage.xaml"));
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "InventoryWorkspacePage.xaml.cs"));
        string mainWindowXaml = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml"));
        string mainWindowSource = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string configureBody = GetSection(
            mainWindowSource,
            "private void ConfigureInventoryWorkspacePage(InventoryWorkspacePage page)",
            "private void SelectWorkspaceNavigationItem(NavigationViewItem navigationItem)");

        Assert.Contains("x:Class=\"P4G.SaveTool.WinUI.InventoryWorkspacePage\"", xaml);
        Assert.Contains("public sealed partial class InventoryWorkspacePage : Page", source, StringComparison.Ordinal);
        Assert.Contains("NavigationCacheMode = NavigationCacheMode.Required;", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InventorySectionHeader\"", xaml);
        Assert.Contains("x:Name=\"InventoryListView\"", xaml);
        Assert.Contains("x:Name=\"InventoryCategoryComboBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryItemComboBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryQuantityTextBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryAddUpdateButton\"", xaml);
        Assert.Contains("x:Name=\"InventoryDeleteButton\"", xaml);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"InventoryListView\"", xaml);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"InventoryCategoryComboBox\"", xaml);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"InventoryItemComboBox\"", xaml);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"InventoryQuantityTextBox\"", xaml);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"InventoryAddUpdateButton\"", xaml);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"InventoryDeleteButton\"", xaml);
        Assert.Contains("Text=\"{Binding}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding DisplayName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("event SelectionChangedEventHandler? InventorySelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("event SelectionChangedEventHandler? InventoryCategorySelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("event SelectionChangedEventHandler? InventoryItemSelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("event TextChangedEventHandler? InventoryQuantityTextChanged", source, StringComparison.Ordinal);
        Assert.Contains("event RoutedEventHandler? InventoryAddUpdateClick", source, StringComparison.Ordinal);
        Assert.Contains("event RoutedEventHandler? InventoryDeleteClick", source, StringComparison.Ordinal);
        Assert.Contains("internal string QuantityText", source, StringComparison.Ordinal);
        Assert.Contains("internal void SetInventoryEnabled(bool isEnabled, bool hasSelectedItem, bool hasSelectedEntry)", source, StringComparison.Ordinal);
        Assert.Contains("page.InventorySelectionChanged += InventoryListView_SelectionChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.InventoryQuantityTextChanged += InventoryQuantityTextBox_TextChanged;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.InventoryAddUpdateClick += InventoryAddUpdateButton_Click;", configureBody, StringComparison.Ordinal);
        Assert.Contains("page.InventoryDeleteClick += InventoryDeleteButton_Click;", configureBody, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"InventorySectionHeader\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"InventoryListView\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"InventoryCategoryComboBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"InventoryItemComboBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"InventoryQuantityTextBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"InventoryAddUpdateButton\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"InventoryDeleteButton\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"EquipmentCharacterComboBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"EquipmentWeaponComboBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"EquipmentArmorComboBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"EquipmentAccessoryComboBox\"", mainWindowXaml);
        Assert.DoesNotContain("x:Name=\"EquipmentCostumeComboBox\"", mainWindowXaml);

        string equipmentXaml = File.ReadAllText(Path.Combine(sourceRoot, "Workspaces", "EquipmentWorkspacePage.xaml"));
        Assert.Contains("x:Name=\"EquipmentCharacterComboBox\"", equipmentXaml);
        Assert.Contains("x:Name=\"EquipmentWeaponComboBox\"", equipmentXaml);
        Assert.Contains("x:Name=\"EquipmentArmorComboBox\"", equipmentXaml);
        Assert.Contains("x:Name=\"EquipmentAccessoryComboBox\"", equipmentXaml);
        Assert.Contains("x:Name=\"EquipmentCostumeComboBox\"", equipmentXaml);
    }

    [Fact]
    public void MainWindowXamlDeclaresShellChromeAndDragDropSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));
        string source = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string dropBody = GetSection(
            source,
            "private async void MainWindow_Drop(object sender, DragEventArgs e)",
            "private static async Task<DataPackageOperation> EvaluateDragOverAcceptanceAsync(DataPackageView dataView)");

        Assert.Contains("Loaded=\"MainWindow_Loaded\"", xaml);
        Assert.Contains("AllowDrop=\"True\"", xaml);
        Assert.Contains("DragOver=\"MainWindow_DragOver\"", xaml);
        Assert.Contains("Drop=\"MainWindow_Drop\"", xaml);
        Assert.Contains("<MenuBar>", xaml);
        Assert.Contains("x:Name=\"FileOpenMenuItem\"", xaml);
        Assert.Contains("Text=\"Open...\"", xaml);
        Assert.Contains("Text=\"Save as...\"", xaml);
        Assert.Contains("Text=\"About\"", xaml);
        Assert.Contains("x:Name=\"AboutButton\"", xaml);
        Assert.Contains("Current save", xaml);
        Assert.Contains("x:Name=\"FilePathTextBlock\"", xaml);
        Assert.Contains("x:Name=\"StateTextBlock\"", xaml);
        Assert.True(
            xaml.IndexOf("x:Name=\"FilePathTextBlock\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"SectionNavigationView\"", StringComparison.Ordinal));
        Assert.Contains("DragOperationDeferral deferral = e.GetDeferral();", dropBody, StringComparison.Ordinal);
        Assert.Contains("deferral.Complete();", dropBody, StringComparison.Ordinal);
        Assert.True(dropBody.IndexOf("DragOperationDeferral deferral = e.GetDeferral();", StringComparison.Ordinal) < dropBody.IndexOf("await e.DataView.GetStorageItemsAsync()", StringComparison.Ordinal));
        Assert.True(dropBody.IndexOf("await RunBusyAsync(() => OpenSaveFileFromPathAsync(openPath, \"Drop\"))", StringComparison.Ordinal) < dropBody.IndexOf("deferral.Complete();", StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindowXamlUsesGalleryShellControlsForStatusAndHierarchy()
    {
        string winUiDirectory = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(winUiDirectory, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(winUiDirectory, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string updateShellStateBody = GetSection(
            source,
            "private void UpdateShellState()",
            "private bool HasPendingEditorDrafts()");
        string displayDiagnosticsBody = GetSection(
            source,
            "private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)",
            "private void SetUiDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)");
        string applyButtonBody = GetSection(
            source,
            "private async void ApplyButton_Click(object sender, RoutedEventArgs e)",
            "private async void SaveButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("xmlns:controls=\"using:CommunityToolkit.WinUI.Controls\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<controls:SettingsCard", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Current save\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<InfoBar", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellStatusInfoBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"ShellStatusInfoBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ProgressBar", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellBusyProgressBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"True\"", xaml, StringComparison.Ordinal);
        Assert.True(
            xaml.IndexOf("x:Name=\"ShellStatusInfoBar\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"SectionNavigationView\"", StringComparison.Ordinal));
        Assert.True(
            xaml.IndexOf("x:Name=\"ShellBusyProgressBar\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"SectionNavigationView\"", StringComparison.Ordinal));

        Assert.Contains("UpdateShellStatusInfoBar(uiDiagnosticsOverride ?? viewModel.Diagnostics);", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellStatusInfoBar(diagnostics);", displayDiagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("ShellBusyProgressBar.Visibility = isOperationBusy ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("ShellStatusInfoBar.Title = \"Busy\";", source, StringComparison.Ordinal);
        Assert.Contains("ShellStatusInfoBar.Title = \"Dirty draft\";", source, StringComparison.Ordinal);
        Assert.Contains("ShellStatusInfoBar.Title = \"Ready to save\";", source, StringComparison.Ordinal);
        Assert.Contains("SaveDiagnostic? primaryDiagnostic = GetPrimaryShellDiagnostic(diagnostics);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SectionNavigationView.Visibility = editorVisibility;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("JumpOverviewButton.IsEnabled = canNavigateOverview;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("JumpBasicStatsButton.IsEnabled = canNavigateEditorSections;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("await RunBusyAsync(", applyButtonBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowNoLongerOwnsNoSaveEmptyState()
    {
        string winUiDirectory = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string xaml = File.ReadAllText(Path.Combine(winUiDirectory, "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string source = File.ReadAllText(Path.Combine(winUiDirectory, "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string updateShellStateBody = GetSection(
            source,
            "private void UpdateShellState()",
            "private void RefreshSocialStatsState()");

        Assert.DoesNotContain("x:Name=\"NoSaveEmptyStateBorder\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"NoSaveOpenButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("NoSaveEmptyStateBorder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NoSaveOpenButton", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LegacyWorkspaceContentStore\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SaveEditorScrollViewer\" Visibility=\"Collapsed\"", xaml, StringComparison.Ordinal);
        Assert.True(
            xaml.IndexOf("x:Name=\"SectionNavigationView\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"SaveEditorScrollViewer\"", StringComparison.Ordinal));

        Assert.Contains("bool hasSave = viewModel.HasSave;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("Visibility editorVisibility = hasSave ? Visibility.Visible : Visibility.Collapsed;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SaveEditorScrollViewer.Visibility = editorVisibility;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("bool canNavigateEditorSections = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("JumpOverviewButton.IsEnabled = canNavigateOverview;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("if (WorkspaceFrame.Content is OverviewWorkspacePage overviewPage)", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("overviewPage.SetOverviewState(CreateOverviewWorkspaceState());", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("JumpDiagnosticsStateButton.IsEnabled = canNavigateEditorSections;", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SectionNavigationRail.Visibility = editorVisibility;", updateShellStateBody, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutDialogXamlExposesLegacyAboutSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "AboutDialog.xaml"));

        Assert.Contains("Title=\"About Version 1.6.0\"", xaml);
        Assert.Contains("NavigateUri=\"http://s15.zetaboards.com/Amicitia/index/\"", xaml);
        Assert.Contains("NavigateUri=\"https://discord.gg/M3uGjsk\"", xaml);
        Assert.Contains("NavigateUri=\"https://github.com/Fennec-kun/P4G-Save-Tool\"", xaml);
        Assert.Contains("NavigateUri=\"https://henkaku.xyz\"", xaml);
    }

    [Fact]
    public void MainWindowXamlExposesPersonaEditorSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"PartySlot0ComboBox\"", xaml);
        Assert.Contains("x:Name=\"PartySlot1ComboBox\"", xaml);
        Assert.Contains("x:Name=\"PartySlot2ComboBox\"", xaml);
        Assert.DoesNotContain("PartySlot0TextBox", xaml);
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
    public void MainWindowPersonaDraftControlsRefreshShellState()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string refreshDraftBody = GetSection(
            source,
            "private void RefreshPersonaDraftShellState(Action? refreshValueText = null)",
            "private void MainCharacterCalculateFromLevelButton_Click(object sender, RoutedEventArgs e)");
        string hasPersonaDraftBody = GetSection(
            source,
            "private bool HasPersonaDraft()",
            "private PersonaSlotViewState? GetSelectedPersonaSlotViewState()");

        Assert.Contains("TextChanged=\"PersonaDraftControl_Changed\"", xaml, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Count(xaml, Regex.Escape("ValueChanged=\"PersonaDraftControl_Changed\"")));
        Assert.Contains("private void PersonaDraftControl_Changed(object sender, TextChangedEventArgs e)", source, StringComparison.Ordinal);
        Assert.Contains("private void PersonaDraftControl_Changed(object sender, RangeBaseValueChangedEventArgs e)", source, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaDraftDiagnostics();", refreshDraftBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", refreshDraftBody, StringComparison.Ordinal);
        Assert.Contains("if (suppressPersonaEvents || viewModel is null || !viewModel.HasSave)", refreshDraftBody, StringComparison.Ordinal);
        Assert.Contains("ReadPersonaId(PersonaChoiceComboBox) != selectedSlot.PersonaId", hasPersonaDraftBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlKeepsSocialStatsCalendarAndSocialLinksOutOfLegacyHost()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.DoesNotContain("x:Name=\"CalendarSocialStatsSectionHeader\"", xaml);
        Assert.DoesNotContain("x:Name=\"CourageComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"KnowledgeComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"ExpressionComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"UnderstandingComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"DiligenceComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"DayTextBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"PhaseComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"NextDayTextBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"NextPhaseComboBox\"", xaml);
        Assert.DoesNotContain("x:Name=\"SocialLinksSectionHeader\"", xaml);
        Assert.DoesNotContain("x:Name=\"SocialLinkListView\"", xaml);
        Assert.Contains("x:Name=\"PartyPersonaSectionHeader\"", xaml);
    }

    [Fact]
    public void MainWindowXamlWiresApplyButtonForWorkspaceEdits()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"ApplyButton\"", xaml);
        Assert.Contains("Click=\"ApplyButton_Click\"", xaml);
        Assert.Contains("x:Name=\"WorkspaceFrame\"", xaml);
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
        string group4InputsBody = GetSection(
            content,
            "private Group4EditInputs GetCurrentGroup4EditInputs()",
            "private SocialStatRankChoiceViewState? GetCurrentSocialStatRankChoice(int statIndex)");
        string hasGroup4DraftBody = GetSection(
            content,
            "private bool HasGroup4Draft()",
            "private bool HasSelectedSocialLinkDraft()");
        string tryReadSocialLinkFieldBody = GetSection(
            content,
            "internal static bool TryReadSocialLinkField(",
            "private string BuildPersonaSummary()");

        Assert.Contains("Group4EditBatchBuilder.TryBuild(", content, StringComparison.Ordinal);
        Assert.Contains("CreateGroup4EditInputs(", content, StringComparison.Ordinal);
        Assert.Contains("AppendGroup4Edits(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("TryAppendSelectedSocialLinkEdits(batch, validationDiagnostics);", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("TryFinalizeEditBatch(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new Group4EditInputs(", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("bool levelIsValid = TryReadSocialLinkField(levelText, \"Level\", \"SocialLinks.Level\", diagnostics, out byte level);", content, StringComparison.Ordinal);
        Assert.Contains("bool progressIsValid = TryReadSocialLinkField(progressText, \"Progress\", \"SocialLinks.Progress\", diagnostics, out byte progress);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("flagIsValid", tryBuildSocialLinkEditsBody, StringComparison.Ordinal);
        Assert.Contains("if (!levelIsValid || !progressIsValid)", tryBuildSocialLinkEditsBody, StringComparison.Ordinal);
        Assert.Contains("diagnostics.Add(CreateUiDiagnostic(\"P4GWINUI024\",", tryReadSocialLinkFieldBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetUiDiagnostics(", tryReadSocialLinkFieldBody, StringComparison.Ordinal);
        Assert.Contains("GetCurrentGroup4EditInputs()", tryBuildEditBatchBody, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(0)", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(1)", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(4)", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(3)", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetSocialStatSelectedRank(2)", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.DayText", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetCalendarPhaseSelectedChoice(false)", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.NextDayText", content, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.GetCalendarPhaseSelectedChoice(true)", content, StringComparison.Ordinal);
        Assert.Contains("GetCurrentSocialStatRankChoice(0)", group4InputsBody, StringComparison.Ordinal);
        Assert.Contains("GetCurrentCalendarPhaseChoice(viewModel.Calendar.DayPhaseId)", group4InputsBody, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage is null", hasGroup4DraftBody, StringComparison.Ordinal);
        Assert.Contains("!calendarSocialStatsWorkspacePageInitializedFromViewModel", hasGroup4DraftBody, StringComparison.Ordinal);
        Assert.Contains("RefreshSocialStatsState();", content, StringComparison.Ordinal);
        Assert.Contains("RefreshCalendarState();", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceSupportsDropOpenAboutAndWindowTitleUpdates()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string dragOverBody = GetSection(
            content,
            "private async void MainWindow_DragOver(object sender, DragEventArgs e)",
            "private async void MainWindow_Drop(object sender, DragEventArgs e)");
        string dropBody = GetSection(
            content,
            "private async void MainWindow_Drop(object sender, DragEventArgs e)",
            "private static async Task<DataPackageOperation> EvaluateDragOverAcceptanceAsync(DataPackageView dataView)");
        string saveBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> SaveAsync(bool forcePicker)",
            "private async Task<string?> PickSavePathAsync()");
        string shellStateBody = GetSection(
            content,
            "private void UpdateShellState()",
            "private void RefreshSocialStatsState()");

        Assert.Contains("private async void MainWindow_Loaded(object sender, RoutedEventArgs e)", content, StringComparison.Ordinal);
        Assert.Contains("private static async Task<DataPackageOperation> EvaluateDragOverAcceptanceAsync(DataPackageView dataView)", content, StringComparison.Ordinal);
        Assert.Contains("if (isBusy)", dragOverBody, StringComparison.Ordinal);
        Assert.True(
            dragOverBody.IndexOf("if (isBusy)", StringComparison.Ordinal) <
            dragOverBody.IndexOf("DragOperationDeferral deferral = e.GetDeferral();", StringComparison.Ordinal));
        Assert.Contains("DragOperationDeferral deferral = e.GetDeferral();", dragOverBody, StringComparison.Ordinal);
        Assert.Contains("DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);", dragOverBody, StringComparison.Ordinal);
        Assert.Contains("e.AcceptedOperation = acceptedOperation;", dragOverBody, StringComparison.Ordinal);
        Assert.Contains("e.AcceptedOperation = DataPackageOperation.None;", dragOverBody, StringComparison.Ordinal);
        Assert.True(
            dragOverBody.IndexOf("DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);", StringComparison.Ordinal) <
            dragOverBody.IndexOf("if (isBusy)", dragOverBody.IndexOf("DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.True(
            dragOverBody.IndexOf("if (isBusy)", dragOverBody.IndexOf("DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);", StringComparison.Ordinal), StringComparison.Ordinal) <
            dragOverBody.IndexOf("e.AcceptedOperation = acceptedOperation;", StringComparison.Ordinal));
        Assert.DoesNotContain("dragOverAcceptanceTask", dragOverBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", dragOverBody, StringComparison.Ordinal);
        Assert.Contains("try", dropBody, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();", dropBody, StringComparison.Ordinal);
        Assert.Contains("if (isBusy)", dropBody, StringComparison.Ordinal);
        Assert.Contains("ShellDragDropHelper.TryGetOpenablePath(items.OfType<StorageFile>().Select(file => file.Path), out string openPath)", dropBody, StringComparison.Ordinal);
        Assert.Contains("await RunBusyAsync(() => OpenSaveFileFromPathAsync(openPath, \"Drop\"));", dropBody, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", dropBody, StringComparison.Ordinal);
        Assert.Contains("await ReportOpenFailureAsync(\"Drop\", $\"Could not read the dropped file: {ex.Message}\");", dropBody, StringComparison.Ordinal);
        Assert.Contains("private async Task<BusyOperationCompletion> OpenSaveFileFromPathAsync(", content, StringComparison.Ordinal);
        Assert.Contains("private async Task ShowAboutDialogAsync()", content, StringComparison.Ordinal);
        Assert.Contains("AboutDialog dialog = new();", content, StringComparison.Ordinal);
        Assert.Contains("dialog.XamlRoot = root.XamlRoot;", content, StringComparison.Ordinal);
        Assert.Contains("UpdateWindowTitle();", saveBody, StringComparison.Ordinal);
        Assert.Contains("UpdateWindowTitle();", shellStateBody, StringComparison.Ordinal);
        Assert.Contains("Title = ShellStateFormatter.GetWindowTitle(currentFilePath);", content, StringComparison.Ordinal);
        Assert.Contains("FilePathTextBlock.Text = ShellStateFormatter.GetFilePathText(currentFilePath);", content, StringComparison.Ordinal);
        Assert.Contains("StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, hasPendingEditorDrafts, viewModel.IsDirty, viewModel.CanWrite);", content, StringComparison.Ordinal);
        Assert.Contains("FileOpenMenuItem.IsEnabled = !isBusy && !startupRefreshPending;", shellStateBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceRefreshesEditorUiAfterSuccessfulOpen()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string openBody = GetSection(
            content,
            "private async Task<BusyOperationCompletion> OpenSaveFileFromPathAsync(string path, string source)",
            "private bool ApplyEditorFields()");

        Assert.Contains("ApplyOpenResult(", openBody, StringComparison.Ordinal);
        Assert.Contains("if (!result.Succeeded)", openBody, StringComparison.Ordinal);
        Assert.Contains("DisplayDiagnostics(result.Diagnostics);", openBody, StringComparison.Ordinal);
        Assert.Contains("if (string.Equals(source, \"Launch\", StringComparison.Ordinal))", openBody, StringComparison.Ordinal);
        Assert.Contains("refreshEditableFieldsAfterStartupOpen = true;", openBody, StringComparison.Ordinal);
        Assert.Contains("return BusyOperationCompletion.PreserveEditorState;", openBody, StringComparison.Ordinal);
        Assert.Contains("return BusyOperationCompletion.RefreshViewModel;", openBody, StringComparison.Ordinal);
        Assert.DoesNotContain("return BusyOperationCompletion.RefreshEditableFields;", openBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowFullRefreshHelperPopulatesInventoryAndDiagnosticsState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string refreshBody = GetSection(
            content,
            "private void RefreshEditableFields()",
            "private void RefreshBasicStatsState()");

        Assert.Contains("RefreshInventoryState();", refreshBody, StringComparison.Ordinal);
        Assert.Contains("RefreshEquipmentState();", refreshBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", refreshBody, StringComparison.Ordinal);
        Assert.Contains("DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);", refreshBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", refreshBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceUsesLaunchArgumentAndDragDropHelpers()
    {
        string appSourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "App.xaml.cs");
        string appContent = File.ReadAllText(appSourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainWindowSourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string mainWindowContent = File.ReadAllText(mainWindowSourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string dragOverBody = GetSection(
            mainWindowContent,
            "private async void MainWindow_DragOver(object sender, DragEventArgs e)",
            "private async void MainWindow_Drop(object sender, DragEventArgs e)");

        Assert.Contains("LaunchArgumentParser.GetOpenPath(args.Arguments, Environment.GetCommandLineArgs())", appContent, StringComparison.Ordinal);
        Assert.Contains("new MainWindow(openPath)", appContent, StringComparison.Ordinal);
        Assert.Contains("private async void OpenButton_Click(object sender, RoutedEventArgs e) =>", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("private async void SaveAsButton_Click(object sender, RoutedEventArgs e) =>", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("bool canSaveAs = canSave;", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("SaveAsButton.IsEnabled = canSaveAs;", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("FileSaveMenuItem.IsEnabled = canSave;", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("FileSaveAsMenuItem.IsEnabled = canSaveAs;", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("SaveButton.IsEnabled = canSave;", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("private async void MainWindow_DragOver(object sender, DragEventArgs e)", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("if (isBusy)", dragOverBody, StringComparison.Ordinal);
        Assert.True(
            dragOverBody.IndexOf("if (isBusy)", StringComparison.Ordinal) <
            dragOverBody.IndexOf("DragOperationDeferral deferral = e.GetDeferral();", StringComparison.Ordinal));
        Assert.Contains("DragOperationDeferral deferral = e.GetDeferral();", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("if (isBusy)", dragOverBody, StringComparison.Ordinal);
        Assert.Contains("private async void MainWindow_Drop(object sender, DragEventArgs e)", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("ShellDragDropHelper.TryGetOpenablePath(items.OfType<StorageFile>().Select(file => file.Path), out string openPath)", mainWindowContent, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", mainWindowContent, StringComparison.Ordinal);
        Assert.DoesNotContain("dragOverAcceptanceTask", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("await ReportOpenFailureAsync(\"Drop\", $\"Could not read the dropped file: {ex.Message}\");", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("ShellStateFormatter.GetWindowTitle(currentFilePath)", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("ShellStateFormatter.GetFilePathText(currentFilePath)", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("ShellStateFormatter.GetStatusText(viewModel.HasSave, hasPendingEditorDrafts, viewModel.IsDirty, viewModel.CanWrite)", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("DiagnosticListItemViewState.FromDiagnostics(diagnostics)", mainWindowContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlWiresSaveAndAboutMenuAndButtons()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("<MenuBar>", content, StringComparison.Ordinal);
        Assert.Contains("<MenuFlyoutItem x:Name=\"FileOpenMenuItem\" Text=\"Open...\" Click=\"OpenButton_Click\" />", content, StringComparison.Ordinal);
        Assert.Contains("<MenuFlyoutItem x:Name=\"FileSaveMenuItem\" Text=\"Save\" Click=\"SaveButton_Click\" IsEnabled=\"False\" />", content, StringComparison.Ordinal);
        Assert.Contains("<MenuFlyoutItem x:Name=\"FileSaveAsMenuItem\" Text=\"Save as...\" Click=\"SaveAsButton_Click\" IsEnabled=\"False\" />", content, StringComparison.Ordinal);
        Assert.Contains("<MenuFlyoutItem Text=\"About\" Click=\"About_Click\" />", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpenButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Content=\"Open save...\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ApplyButton\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SaveButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Content=\"Save\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SaveAsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Content=\"Save as...\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AboutButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"OpenButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"ApplyButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"SaveButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"SaveAsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"AboutButton\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutDialogXamlExposesLegacyBodyCopyAndChrome()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "AboutDialog.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("Title=\"About Version 1.6.0\"", content, StringComparison.Ordinal);
        Assert.Contains("CloseButtonText=\"OK\"", content, StringComparison.Ordinal);
        Assert.Contains("DefaultButton=\"Close\"", content, StringComparison.Ordinal);
        Assert.Contains("Written by Fennec-kun with aid from TGE, Shrinefox, and Pan from", content, StringComparison.Ordinal);
        Assert.Contains("You can contact us live in our Discord server!", content, StringComparison.Ordinal);
        Assert.Contains("Special thanks to you guys who report issues on my GitHub!", content, StringComparison.Ordinal);
        Assert.Contains("This was made possible by", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceHandlesDragDropAcceptanceAndPathRejection()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("private static async Task<DataPackageOperation> EvaluateDragOverAcceptanceAsync(DataPackageView dataView)", content, StringComparison.Ordinal);
        Assert.Contains("DragOperationDeferral deferral = e.GetDeferral();", content, StringComparison.Ordinal);
        Assert.Contains("DataPackageOperation acceptedOperation = await EvaluateDragOverAcceptanceAsync(e.DataView);", content, StringComparison.Ordinal);
        Assert.Contains("if (isBusy)", content, StringComparison.Ordinal);
        Assert.Contains("e.AcceptedOperation = DataPackageOperation.None;", content, StringComparison.Ordinal);
        Assert.Contains("if (!e.DataView.Contains(StandardDataFormats.StorageItems))", content, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();", content, StringComparison.Ordinal);
        Assert.Contains("ShellDragDropHelper.TryGetOpenablePath(items.OfType<StorageFile>().Select(file => file.Path), out string openPath)", content, StringComparison.Ordinal);
        Assert.Contains("await RunBusyAsync(() => OpenSaveFileFromPathAsync(openPath, \"Drop\"));", content, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSourceFormatsShellTitleStatusAndDiagnosticsReset()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string loadedBody = GetSection(
            content,
            "private async void MainWindow_Loaded(object sender, RoutedEventArgs e)",
            "private async void MainWindow_DragOver(object sender, DragEventArgs e)");

        Assert.Contains("UpdateWindowTitle();", loadedBody, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(RefreshBasicFieldsFromViewModel);", loadedBody, StringComparison.Ordinal);
        Assert.True(
            loadedBody.IndexOf("UpdateWindowTitle();", StringComparison.Ordinal) <
            loadedBody.IndexOf("DispatcherQueue.TryEnqueue(RefreshBasicFieldsFromViewModel);", StringComparison.Ordinal));
        Assert.True(
            loadedBody.IndexOf("DispatcherQueue.TryEnqueue(RefreshBasicFieldsFromViewModel);", StringComparison.Ordinal) <
            loadedBody.IndexOf("await RunBusyAsync(", StringComparison.Ordinal));
        Assert.Contains("private async void MainWindow_Loaded(object sender, RoutedEventArgs e)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("if (File.Exists(openPath))", content, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = DispatcherQueue.TryEnqueue(() =>", loadedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("await Task.Delay(5_000);", loadedBody, StringComparison.Ordinal);
        Assert.Contains("if (string.IsNullOrWhiteSpace(startupOpenPath))", loadedBody, StringComparison.Ordinal);
        Assert.Contains("ConsumeStartupOpenPath(ref this.startupOpenPath)", loadedBody, StringComparison.Ordinal);
        Assert.Contains("OpenSaveFileFromPathAsync(startupOpenPath, \"Launch\")", loadedBody, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(RefreshBasicFieldsFromViewModel);", loadedBody, StringComparison.Ordinal);
        Assert.Contains("FilePathTextBlock.Text = ShellStateFormatter.GetFilePathText(currentFilePath);", content, StringComparison.Ordinal);
        Assert.Contains("StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, hasPendingEditorDrafts, viewModel.IsDirty, viewModel.CanWrite);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("if (refreshEditableFieldsAfterStartupOpen && diagnostics.Count == 0)", content, StringComparison.Ordinal);
        string diagnosticsBody = GetSection(
            content,
            "private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)",
            "private void SetUiDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)");
        Assert.Contains("IReadOnlyList<DiagnosticListItemViewState> diagnosticItems = DiagnosticListItemViewState.FromDiagnostics(diagnostics);", diagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("if (DispatcherQueue.HasThreadAccess)", diagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(UpdateDiagnostics);", diagnosticsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherQueue.TryEnqueue(() =>", diagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("ObservableCollection<DiagnosticListItemViewState> diagnosticsItems", content, StringComparison.Ordinal);
        Assert.Contains("public override string ToString()", content, StringComparison.Ordinal);
        Assert.Contains("private static DiagnosticListItemViewState FromDiagnostic(", content, StringComparison.Ordinal);
        Assert.Contains("WorkspaceFrame.Navigate(typeof(DiagnosticsWorkspacePage), diagnosticsItems)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticsListView.ItemsSource = diagnosticsItems;", content, StringComparison.Ordinal);
        Assert.Contains("diagnosticsItems.Clear();", content, StringComparison.Ordinal);
        Assert.Contains("diagnosticsItems.Add(diagnosticItem);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticsListView.ItemsSource = ShellStateFormatter.GetDiagnosticsText(diagnostics);", content, StringComparison.Ordinal);
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "Workspaces", "DiagnosticsWorkspacePage.xaml"));
        Assert.Contains("automation:AutomationProperties.AutomationId=\"DiagnosticsListView\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Diagnostics\"", xaml, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"{Binding AutomationName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Severity}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Code}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Target}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Message}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Title = ShellStateFormatter.GetWindowTitle(currentFilePath);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AppConstructorPreservesXamlBindingPropertiesForNativeAot()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "App.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("XamlBindingPreservation.PreserveXamlBindingProperties();", content, StringComparison.Ordinal);
        Assert.Contains("InitializeComponent();", content, StringComparison.Ordinal);
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
            "private void RefreshSocialLinksState(");

        Assert.Contains("RefreshSocialStatsState();", refreshEditableFieldsBody, StringComparison.Ordinal);
        Assert.Contains("RefreshCalendarState();", refreshEditableFieldsBody, StringComparison.Ordinal);

        Assert.Contains("SetSocialStatSelection(0);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(1);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(4);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(3);", refreshSocialStatsBody, StringComparison.Ordinal);
        Assert.Contains("SetSocialStatSelection(2);", refreshSocialStatsBody, StringComparison.Ordinal);

        Assert.Contains("calendarSocialStatsWorkspacePage.DayText = viewModel.Calendar.Day.ToString(CultureInfo.InvariantCulture);", refreshCalendarBody, StringComparison.Ordinal);
        Assert.Contains("calendarSocialStatsWorkspacePage.NextDayText = viewModel.Calendar.NextDay.ToString(CultureInfo.InvariantCulture);", refreshCalendarBody, StringComparison.Ordinal);
        Assert.Contains(
            "SaveEditorViewModel.GetCalendarPhaseChoices(viewModel.Calendar.DayPhaseId, out CalendarPhaseChoiceViewState selectedPhase)",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "calendarSocialStatsWorkspacePage.SetCalendarPhaseSelection(false, phaseChoices, selectedPhase);",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "SaveEditorViewModel.GetCalendarPhaseChoices(viewModel.Calendar.NextDayPhaseId, out CalendarPhaseChoiceViewState selectedNextPhase)",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "calendarSocialStatsWorkspacePage.SetCalendarPhaseSelection(true, nextPhaseChoices, selectedNextPhase);",
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
        Assert.DoesNotContain(LegacyWpfAssemblyName, directReferences);
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

        Assert.DoesNotContain(LegacyWpfAssemblyName, referencedAssemblies);
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
        AssertProperty(project, "PublishSingleFile", "true");
        AssertProperty(project, "IncludeAllContentForSelfExtract", "true");
        AssertProperty(project, "IncludeNativeLibrariesForSelfExtract", "true");
        AssertProperty(project, "WindowsSdkPackageVersion", RequiredWindowsSdkPackageVersion);
        AssertProperty(project, "CsWinRTWindowsMetadataPackageVersion", RequiredCsWinRTWindowsMetadataPackageVersion);
        AssertProperty(project, "CsWinRTWindowsMetadata", CsWinRTWindowsMetadataPath);
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName == "RuntimeIdentifiers");
    }

    [Fact]
    public void WinUIProjectReferencesCsWinRTSourceGeneratorForNativeAot()
    {
        XDocument project = XDocument.Load(GetWinUIProjectFile());
        string[] directPackageReferences = GetPackageIncludes(project, "PackageReference");
        Assert.Contains(CsWinRTPackageName, directPackageReferences);
        Assert.Contains(CsWinRTWindowsMetadataPackageName, directPackageReferences);
        Assert.Contains(SettingsControlsPackageName, directPackageReferences);

        XDocument centralPackages = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"));
        string packageVersion = GetRequiredPackageVersion(centralPackages, CsWinRTPackageName);
        Version parsedVersion = ParsePackageVersion(packageVersion, CsWinRTPackageName);
        Assert.True(
            parsedVersion.CompareTo(MinimumCsWinRTAotVersion) >= 0,
            $"{CsWinRTPackageName} must be at least {MinimumCsWinRTAotVersion} for WinUI NativeAOT source generation.");
        Assert.Equal(
            RequiredCsWinRTWindowsMetadataPackageVersion,
            GetRequiredPackageVersion(centralPackages, CsWinRTWindowsMetadataPackageName));
        Assert.Equal("8.2.251219", GetRequiredPackageVersion(centralPackages, SettingsControlsPackageName));
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
        AssertProperty(profile, "WindowsPackageType", "None");
        AssertProperty(profile, "EnableMsixTooling", "true");
        AssertProperty(profile, "PublishAot", "true");
        AssertProperty(profile, "PublishSingleFile", "true");
        AssertProperty(profile, "IncludeAllContentForSelfExtract", "true");
        AssertProperty(profile, "IncludeNativeLibrariesForSelfExtract", "true");
        AssertProperty(profile, "SelfContained", "true");
        AssertProperty(profile, "WindowsAppSDKSelfContained", "true");

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
                "IncludeAllContentForSelfExtract",
                "IncludeNativeLibrariesForSelfExtract",
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
        AssertEvaluatedProperty(properties, "PublishSingleFile", "true");
        AssertEvaluatedProperty(properties, "IncludeAllContentForSelfExtract", "true");
        AssertEvaluatedProperty(properties, "IncludeNativeLibrariesForSelfExtract", "true");
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
                "IncludeAllContentForSelfExtract",
                "IncludeNativeLibrariesForSelfExtract",
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
        AssertEvaluatedProperty(properties, "PublishSingleFile", "true");
        AssertEvaluatedProperty(properties, "IncludeAllContentForSelfExtract", "true");
        AssertEvaluatedProperty(properties, "IncludeNativeLibrariesForSelfExtract", "true");
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

    private static void AssertInventoryHandlerGuardsDraftBeforeClearingDraft(string handlerBody)
    {
        int autoApplyIndex = handlerBody.IndexOf("if (!TryGuardSelectedInventoryQuantityDraftBeforeOperation())", StringComparison.Ordinal);
        int clearDraftIndex = handlerBody.IndexOf("inventoryQuantityDraftDirty = false;", StringComparison.Ordinal);

        Assert.True(autoApplyIndex >= 0, "Inventory selection handlers must guard dirty quantity drafts before changing selection.");
        Assert.True(clearDraftIndex < 0 || autoApplyIndex < clearDraftIndex, "Inventory drafts must be guarded before they are cleared.");
        Assert.Contains("RestoreInventorySelectionAfterBlockedDraft();", handlerBody, StringComparison.Ordinal);
    }

    private static void AssertDiagnosticBranchRefreshesShellState(string methodBody, string diagnosticCode)
    {
        int diagnosticIndex = methodBody.IndexOf(diagnosticCode, StringComparison.Ordinal);
        int updateIndex = diagnosticIndex >= 0
            ? methodBody.IndexOf("UpdateShellState();", diagnosticIndex, StringComparison.Ordinal)
            : -1;
        int returnIndex = updateIndex >= 0
            ? methodBody.IndexOf("return;", updateIndex, StringComparison.Ordinal)
            : -1;

        Assert.True(diagnosticIndex >= 0, $"{diagnosticCode} branch was not found.");
        Assert.True(updateIndex > diagnosticIndex, $"{diagnosticCode} branch must refresh shell state after setting diagnostics.");
        Assert.True(returnIndex > updateIndex, $"{diagnosticCode} branch must return after refreshing shell state.");
    }

    private static void AssertPersonaSelectionHandlerGuardsDraftBeforeRefresh(string handlerBody)
    {
        int autoApplyIndex = handlerBody.IndexOf("if (!TryGuardSelectedPersonaDraftBeforeOperation())", StringComparison.Ordinal);
        int refreshIndex = handlerBody.IndexOf("RefreshPersonaState();", StringComparison.Ordinal);

        Assert.True(autoApplyIndex >= 0, "Persona selection handlers must guard dirty persona drafts before changing selection.");
        Assert.Contains("RestorePersonaSelectionAfterBlockedDraft();", handlerBody, StringComparison.Ordinal);
        Assert.True(refreshIndex < 0 || autoApplyIndex < refreshIndex, "Persona drafts must be guarded before refreshing the persona editor.");
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
