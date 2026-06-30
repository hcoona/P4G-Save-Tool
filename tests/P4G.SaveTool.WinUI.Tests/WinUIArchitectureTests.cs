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
        AssertInventoryHandlerAutoAppliesBeforeClearingDraft(listHandlerBody);
        AssertInventoryHandlerAutoAppliesBeforeClearingDraft(categoryHandlerBody);
        AssertInventoryHandlerAutoAppliesBeforeClearingDraft(itemHandlerBody);
        Assert.Contains("TryAutoAddSelectedInventoryItem();", categoryHandlerBody, StringComparison.Ordinal);
        Assert.Contains("TryAutoAddSelectedInventoryItem();", itemHandlerBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.RememberCategoryItem(selectedItem.CategoryId, selectedItem.ItemId);", itemHandlerBody, StringComparison.Ordinal);
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
            "private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)");

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
        AssertPersonaSelectionHandlerAppliesDraftBeforeRefresh(personaMemberHandlerBody);
        AssertPersonaSelectionHandlerAppliesDraftBeforeRefresh(personaSlotHandlerBody);
        AssertPersonaSelectionHandlerAppliesDraftBeforeRefresh(compendiumListHandlerBody);
        AssertPersonaSelectionHandlerAppliesDraftBeforeRefresh(compendiumAddHandlerBody);
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
            "private bool TryApplySelectedSocialLinkDraftBeforeOperation()");
        string autoApplyBody = GetSection(
            content,
            "private bool TryApplySelectedSocialLinkDraftBeforeOperation()",
            "private void RestoreSocialLinkListSelection()");
        string deleteBody = GetSection(
            content,
            "private void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e)",
            "private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e)");

        Assert.Contains("RefreshSocialLinksState();", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkListView_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkAddComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinkApplyButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDeleteButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("if (!TryApplySelectedSocialLinkDraftBeforeOperation())", selectionBody, StringComparison.Ordinal);
        Assert.Contains("RestoreSocialLinkListSelection();", selectionBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryApplySelectedSocialLinkDraftBeforeOperation())", addBody, StringComparison.Ordinal);
        Assert.Contains("ResetSocialLinkAddChoice();", addBody, StringComparison.Ordinal);
        Assert.Contains("RefreshSocialLinksState(allowFallbackSelection: selectedSocialLinkIndex.HasValue);", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedSocialLinkIndex = selectedLink.SlotIndex;", addBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedSocialLinkLinkId = selectedLink.LinkId;", addBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryApplySelectedSocialLinkDraftBeforeOperation())", deleteBody, StringComparison.Ordinal);
        Assert.Contains("if (!TryAppendSelectedSocialLinkEdits(edits, validationDiagnostics))", autoApplyBody, StringComparison.Ordinal);
        Assert.Contains("SetUiDiagnostics(validationDiagnostics);", autoApplyBody, StringComparison.Ordinal);
        Assert.Contains("viewModel.ApplyEdits(edits)", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkLevel(", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkProgress(", autoApplyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetSocialLinkFlag(", autoApplyBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", addBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", deleteBody, StringComparison.Ordinal);
        Assert.Contains("AddSocialLink(", content, StringComparison.Ordinal);
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
            "private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string addBody = GetSection(
            content,
            "private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)",
            "private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)");
        string removeBody = GetSection(
            content,
            "private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");
        string clearBody = GetSection(
            content,
            "private void CompendiumClearButton_Click(object sender, RoutedEventArgs e)",
            "private void RefreshEquipmentState()");
        string helperBody = GetSection(
            content,
            "internal static SaveEditorOperationResult RefreshCompendiumDraftPreservingSelection(",
            "private void RefreshEditableFields()");

        Assert.Contains("if (CompendiumListView.SelectedItem is not CompendiumPersonaViewState selectedEntry)", selectionBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = null;", selectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedPersonaSlotIndex = 0;", selectionBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", selectionBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", selectionBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = selectedEntry.SlotIndex;", selectionBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("CompendiumAddComboBox_SelectionChanged", content, StringComparison.Ordinal);
        Assert.Contains("CompendiumRemoveButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("CompendiumClearButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("RefreshCompendiumState();", content, StringComparison.Ordinal);
        Assert.Contains("selectedChoice.PersonaId == 0", addBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView.SelectedItem = null;", addBody, StringComparison.Ordinal);
        Assert.Contains("RefreshPersonaState();", addBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", addBody, StringComparison.Ordinal);
        Assert.Contains("RefreshCompendiumDraftPreservingSelection(", removeBody, StringComparison.Ordinal);
        Assert.Contains("RefreshCompendiumDraftPreservingSelection(", clearBody, StringComparison.Ordinal);
        Assert.Contains("if (result.Succeeded)", helperBody, StringComparison.Ordinal);
        Assert.Contains("clearSelectedCompendiumSlotIndex();", helperBody, StringComparison.Ordinal);
        Assert.Contains("refreshFromViewModelPreservingInventoryQuantityDraft(!result.Succeeded);", helperBody, StringComparison.Ordinal);
        Assert.Contains("SelectOrAddCompendiumPersona(", content, StringComparison.Ordinal);
        Assert.Contains("TryResolveCompendiumPersonaAddTarget(", content, StringComparison.Ordinal);
        Assert.Contains("SelectOrAddCompendiumPersonaCore(", content, StringComparison.Ordinal);
        Assert.Contains("ResolveSelectedPersonaSlotIndexForProtagonistView(", content, StringComparison.Ordinal);
        Assert.Contains("int? selectedCompendiumSlotIndexBeforeMutation = selectedCompendiumSlotIndex;", addBody, StringComparison.Ordinal);
        Assert.Contains("ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(", addBody, StringComparison.Ordinal);
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

        Assert.Contains("InventoryQuantityTextBox.IsEnabled = canEdit && selectedInventoryItemId.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("InventoryAddUpdateButton.IsEnabled = canEdit && selectedInventoryItemId.HasValue;", updateShellStateBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowCompendiumNoSaveHandlersReturnP4GWINUI025BeforeMutatingState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string removeBody = GetSection(
            content,
            "private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");
        string clearBody = GetSection(
            content,
            "private void CompendiumClearButton_Click(object sender, RoutedEventArgs e)",
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
            "private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e)",
            "private void CompendiumClearButton_Click(object sender, RoutedEventArgs e)");

        string noSelectionDiagnostic = "SetUiDiagnostics([CreateUiDiagnostic(\"P4GWINUI026\", \"Select a compendium entry before removing it.\", \"Compendium.Item\")]);";
        Assert.Contains("if (!selectedCompendiumSlotIndex.HasValue)", removeBody, StringComparison.Ordinal);
        Assert.Contains(noSelectionDiagnostic, removeBody, StringComparison.Ordinal);
        Assert.True(removeBody.IndexOf(noSelectionDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("uiDiagnosticsOverride = null;", StringComparison.Ordinal));
        Assert.True(removeBody.IndexOf(noSelectionDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("RefreshCompendiumDraftPreservingSelection(", StringComparison.Ordinal));
        Assert.True(removeBody.IndexOf(noSelectionDiagnostic, StringComparison.Ordinal) < removeBody.IndexOf("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", StringComparison.Ordinal));
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
        Assert.Contains("ResolveSelectedCompendiumViewState(", refreshCompendiumBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectCompendiumEntryAfterOpen", refreshCompendiumBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = selectedEntry.SlotIndex;", refreshCompendiumBody, StringComparison.Ordinal);
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

        Assert.Contains("new SetMainCharacterLevelEdit((byte)MainCharacterLevelSlider.Value)", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("new SetMainCharacterTotalExperienceEdit(parsedMainCharacterTotalExperience)", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("P4GWINUI028", editBatchBody, StringComparison.Ordinal);
        Assert.Contains("LevelExperienceProjection.CalculateTotalExperienceFromLevel", content, StringComparison.Ordinal);
        Assert.Contains("SetLevelSliderValue(MainCharacterLevelSlider, viewModel.HasSave ? viewModel.MainCharacterLevel : 0);", refreshBasicStatsBody, StringComparison.Ordinal);
        Assert.Contains("UpdateMainCharacterLevelValueText();", refreshBasicStatsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Max(1d, viewModel.MainCharacterLevel)", refreshBasicStatsBody, StringComparison.Ordinal);
        Assert.Contains("NavigateToSection(BasicStatsSectionHeader);", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToSection(CalendarSocialStatsSectionHeader);", content, StringComparison.Ordinal);
        Assert.Contains("NavigateToSection(DiagnosticsStateSectionHeader);", content, StringComparison.Ordinal);
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
            "private void SocialStatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
        string dayBody = GetSection(
            content,
            "private void ApplyImmediateDayEdit(string text, bool isNextDay)",
            "private void PhaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)");
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
    public void MainWindowXamlDeclaresSocialLinkEditingControls()
    {
        string xamlFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml");
        string content = File.ReadAllText(xamlFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string mainCharacterLevelSlider = GetSection(
            content,
            "x:Name=\"MainCharacterLevelSlider\"",
            "x:Name=\"MainCharacterLevelValueTextBlock\"");

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
        string mainCharacterLevelSlider = GetSection(
            content,
            "x:Name=\"MainCharacterLevelSlider\"",
            "x:Name=\"MainCharacterLevelValueTextBlock\"");
        string personaLevelSlider = GetSection(
            content,
            "x:Name=\"PersonaLevelSlider\"",
            "x:Name=\"PersonaLevelValueTextBlock\"");

        Assert.Contains("x:Name=\"JumpBasicStatsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpBasicStats_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpCalendarSocialStatsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpCalendarSocialStats_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpSocialLinksButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpSocialLinks_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpPartyPersonaButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpPartyPersona_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpEquipmentButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpEquipment_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpCompendiumButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpCompendium_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpInventoryButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpInventory_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JumpDiagnosticsStateButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"JumpDiagnosticsState_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BasicStatsSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CalendarSocialStatsSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SocialLinksSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PartyPersonaSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EquipmentSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DiagnosticsStateSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompendiumSectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InventorySectionHeader\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FamilyNameTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GivenNameTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"YenTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterLevelSlider\"", content, StringComparison.Ordinal);
        Assert.Contains("Minimum=\"0\"", mainCharacterLevelSlider, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterLevelValueTextBlock\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterTotalExperienceTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainCharacterCalculateFromLevelButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Click=\"MainCharacterCalculateFromLevelButton_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("ValueChanged=\"MainCharacterLevelSlider_ValueChanged\"", content, StringComparison.Ordinal);
        Assert.Contains("MainCharacterCalculateFromLevelButton_Click", content, StringComparison.Ordinal);
        Assert.Contains("MainCharacterLevelSlider_ValueChanged", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PersonaCalculateFromLevelButton\"", content, StringComparison.Ordinal);
        Assert.Contains("Minimum=\"0\"", personaLevelSlider, StringComparison.Ordinal);
        Assert.Contains("Click=\"PersonaCalculateFromLevelButton_Click\"", content, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PersonaLevelValueTextBlock\"", content, StringComparison.Ordinal);
        Assert.Contains("ValueChanged=\"PersonaLevelSlider_ValueChanged\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=MainCharacterLevelSlider", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementName=PersonaLevelSlider", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"JumpBasicStatsButton\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"FamilyNameTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"GivenNameTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"YenTextBox\"", content, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", content, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"960\"", content, StringComparison.Ordinal);
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
            "              x:Name=\"InventorySectionHeader\"");

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
        Assert.DoesNotContain("SocialLinkFlagTextBox", updateShellStateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialLinkApplyButton", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDeleteButton.IsEnabled = canEdit && selectedSocialLinkIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumListView.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumAddComboBox.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumRemoveButton.IsEnabled = canEdit && selectedCompendiumSlotIndex.HasValue;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumClearButton.IsEnabled = canEdit && compendiumItems.Count > 0;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("MainCharacterLevelSlider.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("MainCharacterTotalExperienceTextBox.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
        Assert.Contains("MainCharacterCalculateFromLevelButton.IsEnabled = canEdit;", updateShellStateBody, StringComparison.Ordinal);
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
        Assert.Contains("if (!ApplyEditorFields())", saveBody, StringComparison.Ordinal);
        Assert.True(
            saveBody.IndexOf("targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)", StringComparison.Ordinal) <
            saveBody.IndexOf("if (!ApplyEditorFields())", StringComparison.Ordinal));
        Assert.DoesNotContain("CreateBlankSave", saveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreNoSaveStateAfterFailedBlankSave", saveBody, StringComparison.Ordinal);
        Assert.Contains("if (string.IsNullOrWhiteSpace(targetPath))", saveBody, StringComparison.Ordinal);
        Assert.Contains("RefreshFromViewModelPreservingInventoryQuantityDraft();", saveBody, StringComparison.Ordinal);
        Assert.Contains("currentFilePath = targetPath;", saveBody, StringComparison.Ordinal);
        Assert.Contains("saveEditorRefreshCoordinator.RunWithFullRefreshSuppressed(", saveBody, StringComparison.Ordinal);
        Assert.Contains("return BusyOperationCompletion.PreserveEditorState;", saveBody, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(saveBody, Regex.Escape("RefreshFromViewModelPreservingInventoryQuantityDraft();")));
        Assert.Contains("string inventoryQuantityDraft = InventoryQuantityTextBox.Text;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("SocialLinkDraftState? socialLinkDraft = CaptureSelectedSocialLinkDraft();", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("CompendiumDraftState? compendiumDraft = preserveSelectedCompendiumDraft ? CaptureSelectedCompendiumDraft() : null;", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("InventorySelectionState.ShouldRestoreQuantityDraft(", inventoryRefreshBody, StringComparison.Ordinal);
        Assert.Contains("InventoryQuantityTextBox.Text = inventoryQuantityDraft;", inventoryRefreshBody, StringComparison.Ordinal);
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
            "private void InventoryDeleteButton_Click(");

        Assert.Contains("if (quantity == 0)", methodBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowImmediateZeroQuantityInventoryEditSuppressesAutoSelectLikeDelete()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void InventoryQuantityTextBox_TextChanged(",
            "private void FamilyNameTextBox_TextChanged(");

        Assert.Contains("if (quantity == 0)", methodBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", methodBody, StringComparison.Ordinal);
        Assert.Contains("RefreshInventoryState();", methodBody, StringComparison.Ordinal);
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
        Assert.Contains("selectedPersonaMemberId = 0;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedPersonaSlotIndex = 0;", openBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.Reset();", openBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectInventoryEntryAfterOpen = true;", openBody, StringComparison.Ordinal);
        Assert.Contains("autoSelectCompendiumEntryAfterOpen = true;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedCompendiumSlotIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedSocialLinkIndex = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("selectedSocialLinkLinkId = null;", openBody, StringComparison.Ordinal);
        Assert.Contains("InventoryQuantityTextBox.Text = string.Empty;", openBody, StringComparison.Ordinal);
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
        Assert.Contains("Text=\"{Binding}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding DisplayName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EquipmentCharacterComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentWeaponComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentArmorComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentAccessoryComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentCostumeComboBox\"", xaml);
    }

    [Fact]
    public void MainWindowXamlDeclaresShellChromeAndDragDropSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

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
            "private string BuildPersonaSummary()");

        Assert.Contains("Group4EditBatchBuilder.TryBuild(", content, StringComparison.Ordinal);
        Assert.Contains("CreateGroup4EditInputs(", tryBuildEditBatchBody, StringComparison.Ordinal);
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
        Assert.Contains("StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, viewModel.IsDirty || HasPendingEditorDrafts(), viewModel.CanWrite);", content, StringComparison.Ordinal);
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
        Assert.Contains("ShellStateFormatter.GetStatusText(viewModel.HasSave, viewModel.IsDirty || HasPendingEditorDrafts(), viewModel.CanWrite)", mainWindowContent, StringComparison.Ordinal);
        Assert.Contains("ShellStateFormatter.GetDiagnosticsText(diagnostics)", mainWindowContent, StringComparison.Ordinal);
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
        Assert.Contains("<Button x:Name=\"OpenButton\" Content=\"Open save...\" Click=\"OpenButton_Click\" />", content, StringComparison.Ordinal);
        Assert.Contains("<Button x:Name=\"SaveButton\" Content=\"Save\" Click=\"SaveButton_Click\" IsEnabled=\"False\" />", content, StringComparison.Ordinal);
        Assert.Contains("<Button x:Name=\"SaveAsButton\" Content=\"Save as...\" Click=\"SaveAsButton_Click\" IsEnabled=\"False\" />", content, StringComparison.Ordinal);
        Assert.Contains("<Button x:Name=\"AboutButton\" Content=\"About\" Click=\"About_Click\" />", content, StringComparison.Ordinal);
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
        Assert.Contains("StateTextBlock.Text = ShellStateFormatter.GetStatusText(viewModel.HasSave, viewModel.IsDirty || HasPendingEditorDrafts(), viewModel.CanWrite);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("if (refreshEditableFieldsAfterStartupOpen && diagnostics.Count == 0)", content, StringComparison.Ordinal);
        string diagnosticsBody = GetSection(
            content,
            "private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)",
            "private void SetUiDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)");
        Assert.Contains("IReadOnlyList<string> diagnosticsText = ShellStateFormatter.GetDiagnosticsText(diagnostics);", diagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("if (DispatcherQueue.HasThreadAccess)", diagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(UpdateDiagnostics);", diagnosticsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherQueue.TryEnqueue(() =>", diagnosticsBody, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsListView.ItemsSource = diagnosticsItems;", content, StringComparison.Ordinal);
        Assert.Contains("diagnosticsItems.Clear();", content, StringComparison.Ordinal);
        Assert.Contains("diagnosticsItems.Add(diagnosticText);", content, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticsListView.ItemsSource = ShellStateFormatter.GetDiagnosticsText(diagnostics);", content, StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.AutomationId=\"DiagnosticsListView\"", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml")), StringComparison.Ordinal);
        Assert.Contains("automation:AutomationProperties.Name=\"Diagnostics\"", File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml")), StringComparison.Ordinal);
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
            "PhaseComboBox.Items.Clear();",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "PhaseComboBox.Items.Add(choice);",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "NextPhaseComboBox.Items.Clear();",
            refreshCalendarBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "NextPhaseComboBox.Items.Add(choice);",
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

    private static void AssertInventoryHandlerAutoAppliesBeforeClearingDraft(string handlerBody)
    {
        int autoApplyIndex = handlerBody.IndexOf("if (!TryApplySelectedInventoryQuantityDraftBeforeOperation())", StringComparison.Ordinal);
        int clearDraftIndex = handlerBody.IndexOf("inventoryQuantityDraftDirty = false;", StringComparison.Ordinal);

        Assert.True(autoApplyIndex >= 0, "Inventory selection handlers must auto-apply dirty quantity drafts before changing selection.");
        Assert.True(clearDraftIndex < 0 || autoApplyIndex < clearDraftIndex, "Inventory drafts must be applied before they are cleared.");
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

    private static void AssertPersonaSelectionHandlerAppliesDraftBeforeRefresh(string handlerBody)
    {
        int autoApplyIndex = handlerBody.IndexOf("if (!TryApplySelectedPersonaDraftBeforeOperation())", StringComparison.Ordinal);
        int refreshIndex = handlerBody.IndexOf("RefreshPersonaState();", StringComparison.Ordinal);

        Assert.True(autoApplyIndex >= 0, "Persona selection handlers must apply dirty persona drafts before changing selection.");
        Assert.Contains("RestorePersonaSelectionAfterBlockedDraft();", handlerBody, StringComparison.Ordinal);
        Assert.True(refreshIndex < 0 || autoApplyIndex < refreshIndex, "Persona drafts must be applied before refreshing the persona editor.");
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
