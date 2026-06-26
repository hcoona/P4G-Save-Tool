using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class CompendiumSelectionTests
{
    [Fact]
    public void CompendiumSelectionResolverAutoSelectsFirstVisibleEntryWhenRequested()
    {
        IReadOnlyList<CompendiumPersonaViewState> compendiumEntries =
        [
            new(3, 0x0101, "First", 12, 100),
            new(7, 0x0202, "Second", 18, 200),
        ];

        CompendiumPersonaViewState? selectedEntry = MainWindow.ResolveSelectedCompendiumViewState(
            compendiumEntries,
            null,
            autoSelectFirstVisibleEntry: true);

        Assert.Same(compendiumEntries[0], selectedEntry);
    }

    [Fact]
    public void CompendiumSelectionResolverFallsBackToFirstVisibleEntryWhenSelectionIsStale()
    {
        IReadOnlyList<CompendiumPersonaViewState> compendiumEntries =
        [
            new(3, 0x0101, "First", 12, 100),
            new(7, 0x0202, "Second", 18, 200),
        ];

        CompendiumPersonaViewState? selectedEntry = MainWindow.ResolveSelectedCompendiumViewState(
            compendiumEntries,
            selectedCompendiumSlotIndex: 99,
            autoSelectFirstVisibleEntry: true);

        Assert.Same(compendiumEntries[0], selectedEntry);
    }

    [Fact]
    public void CompendiumSelectionResolverPreservesExistingSelectionWhenPresent()
    {
        IReadOnlyList<CompendiumPersonaViewState> compendiumEntries =
        [
            new(3, 0x0101, "First", 12, 100),
            new(7, 0x0202, "Second", 18, 200),
        ];

        CompendiumPersonaViewState? selectedEntry = MainWindow.ResolveSelectedCompendiumViewState(
            compendiumEntries,
            selectedCompendiumSlotIndex: 7,
            autoSelectFirstVisibleEntry: true);

        Assert.Same(compendiumEntries[1], selectedEntry);
    }

    [Fact]
    public void CompendiumSelectionResolverLeavesSelectionClearedWhenAutoSelectIsNotRequested()
    {
        IReadOnlyList<CompendiumPersonaViewState> compendiumEntries =
        [
            new(3, 0x0101, "First", 12, 100),
            new(7, 0x0202, "Second", 18, 200),
        ];

        CompendiumPersonaViewState? selectedEntry = MainWindow.ResolveSelectedCompendiumViewState(
            compendiumEntries,
            selectedCompendiumSlotIndex: null,
            autoSelectFirstVisibleEntry: false);

        Assert.Null(selectedEntry);
    }

    [Fact]
    public void CompendiumSelectionResolverReturnsNullWhenNoEntriesExist()
    {
        CompendiumPersonaViewState? selectedEntry = MainWindow.ResolveSelectedCompendiumViewState(
            Array.Empty<CompendiumPersonaViewState>(),
            selectedCompendiumSlotIndex: null,
            autoSelectFirstVisibleEntry: true);

        Assert.Null(selectedEntry);
    }

    [Fact]
    public void CompendiumAddTargetResolverPrefersExistingSlotOverFreeSlot()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
            new(2, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            0x1111,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.True(succeeded);
        Assert.Equal(0, slotIndex);
        Assert.True(existingSlot);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CompendiumAddTargetResolverPrefersExistingSlotEvenWhenFreeSlotComesFirst()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
            new(1, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(2, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            0x1111,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.True(succeeded);
        Assert.Equal(1, slotIndex);
        Assert.True(existingSlot);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CompendiumAddTargetResolverReturnsExistingSlotWhenCompendiumIsFull()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            0x2222,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.True(succeeded);
        Assert.Equal(1, slotIndex);
        Assert.True(existingSlot);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CompendiumSelectionHelperSelectsExistingSlotWithoutEmittingEdit()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(0x1111, "Existing Persona");
        int selectedSlotIndex = -1;
        int editCallCount = 0;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(0, selectedSlotIndex);
        Assert.Equal(0, editCallCount);
    }

    [Fact]
    public void CompendiumSelectionHelperSelectsExistingSlotWithoutAddingWhenCompendiumIsFull()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
        ];

        PersonaChoiceViewState selectedChoice = new(0x2222, "Existing Persona");
        int selectedSlotIndex = -1;
        int editCallCount = 0;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, selectedSlotIndex);
        Assert.Equal(0, editCallCount);
    }

    [Fact]
    public void CompendiumAddTargetResolverUsesPersonaIdSlotWhenInRangeEvenIfEarlierSlotIsFree()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
            new(2, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            3,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.True(succeeded);
        Assert.Equal(2, slotIndex);
        Assert.False(existingSlot);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CompendiumAddTargetResolverFallsBackToFreeSlotWhenLegacySlotIsOccupiedByDifferentPersona()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
            new(2, true, 0x4444, 14, 102, [1, 2, 3, 4, 5, 6, 7, 8], 12, 13, 14, 15, 16),
            new(3, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            3,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.True(succeeded);
        Assert.Equal(3, slotIndex);
        Assert.False(existingSlot);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CompendiumAddTargetResolverFailsWhenLegacySlotIsOccupiedByDifferentPersonaAndNoFreeSlotExists()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
            new(2, true, 0x4444, 14, 102, [1, 2, 3, 4, 5, 6, 7, 8], 12, 13, 14, 15, 16),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            3,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.False(succeeded);
        Assert.Equal(-1, slotIndex);
        Assert.False(existingSlot);
        Assert.NotNull(diagnostic);
        Assert.Equal("P4GWINUI027", diagnostic!.Code);
        Assert.Equal("Compendium", diagnostic.Target);
    }

    [Fact]
    public void CompendiumSelectionHelperAddsIntoPersonaIdSlotAndSelectsIt()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
            new(2, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(3, "New Persona");
        int selectedSlotIndex = -1;
        int editCallCount = 0;
        PersonaSlotEdit? appliedEdit = null;
        bool appliedEditCaptured = false;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (slotIndex, personaSlotEdit) =>
            {
                editCallCount++;
                appliedEdit = personaSlotEdit;
                appliedEditCaptured = true;
                Assert.Equal(2, slotIndex);
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, selectedSlotIndex);
        Assert.Equal(1, editCallCount);
        Assert.True(appliedEditCaptured);
        PersonaSlotEdit appliedEditValue = Assert.IsType<PersonaSlotEdit>(appliedEdit);
        Assert.Equal((ushort)3, appliedEditValue.PersonaId);
        Assert.Equal((byte)1, appliedEditValue.Level);
        Assert.Equal((uint)0, appliedEditValue.TotalExperience);
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0], appliedEditValue.SkillIds);
        Assert.Equal((byte)1, appliedEditValue.Strength);
        Assert.Equal((byte)1, appliedEditValue.Magic);
        Assert.Equal((byte)1, appliedEditValue.Endurance);
        Assert.Equal((byte)1, appliedEditValue.Agility);
        Assert.Equal((byte)1, appliedEditValue.Luck);
    }

    [Fact]
    public void CompendiumSelectionHelperAddsIntoFreeSlotWhenLegacySlotIsOccupiedByDifferentPersona()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
            new(2, true, 0x4444, 14, 102, [1, 2, 3, 4, 5, 6, 7, 8], 12, 13, 14, 15, 16),
            new(3, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(3, "New Persona");
        int selectedSlotIndex = -1;
        int editCallCount = 0;
        int appliedSlotIndex = -1;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (slotIndex, personaSlotEdit) =>
            {
                editCallCount++;
                appliedSlotIndex = slotIndex;
                Assert.Equal((ushort)3, personaSlotEdit.PersonaId);
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, selectedSlotIndex);
        Assert.Equal(3, appliedSlotIndex);
        Assert.Equal(1, editCallCount);
    }

    [Fact]
    public void CompendiumSelectionHelperTreatsBlankChoiceAsNoOp()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(0, "Blank");
        int selectedSlotIndex = 42;
        int editCallCount = 0;
        int selectionCallCount = 0;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex =>
            {
                selectionCallCount++;
                selectedSlotIndex = slotIndex;
            });

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(42, selectedSlotIndex);
        Assert.Equal(0, editCallCount);
        Assert.Equal(0, selectionCallCount);
    }

    [Fact]
    public void ResolveSelectedPersonaSlotIndexForProtagonistViewPreservesPreviousSelection()
    {
        IReadOnlyList<PersonaSlotViewState> personaSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
            new(2, true, 0x3333, 14, 102, [1, 2, 3, 4, 5, 6, 7, 8], 12, 13, 14, 15, 16),
        ];

        int selectedPersonaSlotIndexBeforeCompendium = 2;
        int selectedPersonaSlotIndexAfterCompendium = selectedPersonaSlotIndexBeforeCompendium;

        int resolvedPersonaSlotIndex = MainWindow.ResolveSelectedPersonaSlotIndexForProtagonistView(
            selectedPersonaSlotIndexAfterCompendium,
            personaSlots);

        Assert.Equal(selectedPersonaSlotIndexBeforeCompendium, resolvedPersonaSlotIndex);
    }

    [Fact]
    public void PreserveSelectedPersonaSelectionDuringCompendiumRefreshPreservesExistingMemberAndSlotContext()
    {
        byte? selectedPersonaMemberId = 2;
        int selectedPersonaSlotIndex = 3;

        (byte? preservedMemberId, int preservedSlotIndex) =
            MainWindow.PreserveSelectedPersonaSelectionDuringCompendiumRefresh(
                selectedPersonaMemberId,
                selectedPersonaSlotIndex);

        Assert.Equal(selectedPersonaMemberId, preservedMemberId);
        Assert.Equal(selectedPersonaSlotIndex, preservedSlotIndex);
    }

    [Fact]
    public void ClearSelectedCompendiumContextClearsSelectedCompendiumSlotOnly()
    {
        int? selectedCompendiumSlotIndex = 7;

        MainWindow.ClearSelectedCompendiumContext(ref selectedCompendiumSlotIndex);

        Assert.Null(selectedCompendiumSlotIndex);
    }

    [Fact]
    public void CompendiumSelectionHelperReturnsP4GWINUI027WhenCompendiumIsFullAndPersonaIsMissing()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
        ];

        PersonaChoiceViewState selectedChoice = new(0xBEEF, "Missing Persona");
        int selectedSlotIndex = 7;
        int editCallCount = 0;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.False(result.Succeeded);
        Assert.Single(result.Diagnostics);
        Assert.Equal("P4GWINUI027", result.Diagnostics[0].Code);
        Assert.Equal(7, selectedSlotIndex);
        Assert.Equal(0, editCallCount);
    }

    [Fact]
    public void CompendiumSelectionHelperLeavesSelectionUnchangedWhenFreeSlotAddFails()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(0xBEEF, "New Persona");
        int selectedSlotIndex = 7;
        int editCallCount = 0;

        SaveEditorOperationResult result = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(
                    false,
                    [new SaveDiagnostic(DiagnosticSeverity.Error, "P4GTEST001", "Failed to add compendium persona.", "Compendium")]);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.False(result.Succeeded);
        Assert.Single(result.Diagnostics);
        Assert.Equal("P4GTEST001", result.Diagnostics[0].Code);
        Assert.Equal(7, selectedSlotIndex);
        Assert.Equal(1, editCallCount);
    }

    [Fact]
    public void CompendiumAddTargetResolverUsesFirstFreeSlotForHighPersonaIds()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
            new(2, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            0xBEEF,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.True(succeeded);
        Assert.Equal(1, slotIndex);
        Assert.False(existingSlot);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void CompendiumAddTargetResolverFailsCleanlyWhenCompendiumIsFull()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
        ];

        bool succeeded = MainWindow.TryResolveCompendiumPersonaAddTarget(
            compendiumSlots,
            0xBEEF,
            out int slotIndex,
            out bool existingSlot,
            out SaveDiagnostic? diagnostic);

        Assert.False(succeeded);
        Assert.Equal(-1, slotIndex);
        Assert.False(existingSlot);
        Assert.NotNull(diagnostic);
        Assert.Equal("P4GWINUI027", diagnostic!.Code);
        Assert.Equal("Compendium", diagnostic.Target);
    }

    [Fact]
    public void CompendiumDraftRefreshHelperRestoresDraftWhenSelectedSlotContextIsStable()
    {
        MainWindow.CompendiumDraftState draft = new(
            3,
            0x0404,
            "1234",
            55,
            11,
            22,
            33,
            44,
            45,
            0x4401,
            0x4402,
            0x4403,
            0x4404,
            0x4405,
            0x4406,
            0x4407,
            0x4408);

        ushort selectedPersonaId = 0x0101;
        string experienceText = "999";
        double level = 1;
        double strength = 2;
        double magic = 3;
        double endurance = 4;
        double agility = 5;
        double luck = 6;
        ushort[] skillIds = [1, 2, 3, 4, 5, 6, 7, 8];

        SimulateCompendiumDraftRefresh(
            draft,
            draft.SlotIndex,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds);

        Assert.Equal(draft.PersonaId, selectedPersonaId);
        Assert.Equal(draft.ExperienceText, experienceText);
        Assert.Equal(draft.Level, level);
        Assert.Equal(draft.Strength, strength);
        Assert.Equal(draft.Magic, magic);
        Assert.Equal(draft.Endurance, endurance);
        Assert.Equal(draft.Agility, agility);
        Assert.Equal(draft.Luck, luck);
        Assert.Equal(new[] { draft.Skill1Id, draft.Skill2Id, draft.Skill3Id, draft.Skill4Id, draft.Skill5Id, draft.Skill6Id, draft.Skill7Id, draft.Skill8Id }, skillIds);
    }

    [Fact]
    public void CompendiumDraftRefreshHelperSkipsRestoreWhenSelectionContextChangesOrClears()
    {
        MainWindow.CompendiumDraftState draft = new(
            3,
            0x0404,
            "1234",
            55,
            11,
            22,
            33,
            44,
            45,
            0x4401,
            0x4402,
            0x4403,
            0x4404,
            0x4405,
            0x4406,
            0x4407,
            0x4408);

        ushort selectedPersonaId = 0x0101;
        string experienceText = "committed";
        double level = 10;
        double strength = 20;
        double magic = 30;
        double endurance = 40;
        double agility = 50;
        double luck = 60;
        ushort[] skillIds = [9, 10, 11, 12, 13, 14, 15, 16];

        SimulateCompendiumDraftRefresh(
            draft,
            4,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds);

        Assert.Equal((ushort)0x0101, selectedPersonaId);
        Assert.Equal("committed", experienceText);
        Assert.Equal(10d, level);
        Assert.Equal(20d, strength);
        Assert.Equal(30d, magic);
        Assert.Equal(40d, endurance);
        Assert.Equal(50d, agility);
        Assert.Equal(60d, luck);
        Assert.Equal(new[] { (ushort)9, (ushort)10, (ushort)11, (ushort)12, (ushort)13, (ushort)14, (ushort)15, (ushort)16 }, skillIds);

        selectedPersonaId = 0x0101;
        experienceText = "committed";
        level = 10;
        strength = 20;
        magic = 30;
        endurance = 40;
        agility = 50;
        luck = 60;
        skillIds = [9, 10, 11, 12, 13, 14, 15, 16];

        SimulateCompendiumDraftRefresh(
            draft,
            null,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds);

        Assert.Equal((ushort)0x0101, selectedPersonaId);
        Assert.Equal("committed", experienceText);
        Assert.Equal(10d, level);
        Assert.Equal(20d, strength);
        Assert.Equal(30d, magic);
        Assert.Equal(40d, endurance);
        Assert.Equal(50d, agility);
        Assert.Equal(60d, luck);
        Assert.Equal(new[] { (ushort)9, (ushort)10, (ushort)11, (ushort)12, (ushort)13, (ushort)14, (ushort)15, (ushort)16 }, skillIds);
    }

    [Fact]
    public void CompendiumAddRefreshPreservesDraftWhenExistingSlotSelectionIsANoOp()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(0x1111, "Existing Persona");
        int selectedSlotIndex = 0;
        int editCallCount = 0;

        SaveEditorOperationResult addResult = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(true, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(addResult.Succeeded);
        Assert.Empty(addResult.Diagnostics);
        Assert.Equal(0, selectedSlotIndex);
        Assert.Equal(0, editCallCount);

        MainWindow.CompendiumDraftState draft = new(
            0,
            0x1111,
            "draft-xp",
            12,
            13,
            14,
            15,
            16,
            17,
            0x2101,
            0x2102,
            0x2103,
            0x2104,
            0x2105,
            0x2106,
            0x2107,
            0x2108);

        ushort selectedPersonaId = 0x2222;
        string experienceText = "committed";
        double level = 22;
        double strength = 23;
        double magic = 24;
        double endurance = 25;
        double agility = 26;
        double luck = 27;
        ushort[] skillIds = [9, 10, 11, 12, 13, 14, 15, 16];

        SimulateCompendiumDraftRefresh(
            draft,
            selectedSlotIndex,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds);

        Assert.Equal(draft.PersonaId, selectedPersonaId);
        Assert.Equal(draft.ExperienceText, experienceText);
        Assert.Equal(draft.Level, level);
        Assert.Equal(draft.Strength, strength);
        Assert.Equal(draft.Magic, magic);
        Assert.Equal(draft.Endurance, endurance);
        Assert.Equal(draft.Agility, agility);
        Assert.Equal(draft.Luck, luck);
        Assert.Equal(new[] { draft.Skill1Id, draft.Skill2Id, draft.Skill3Id, draft.Skill4Id, draft.Skill5Id, draft.Skill6Id, draft.Skill7Id, draft.Skill8Id }, skillIds);
    }

    [Fact]
    public void CompendiumAddRefreshDoesNotRestoreDraftWhenSameSlotMutationChangesPersonaData()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
            new(2, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(3, "New Persona");
        int selectedSlotIndex = 2;

        SaveEditorOperationResult addResult = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) => new SaveEditorOperationResult(true, []),
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(addResult.Succeeded);
        Assert.Empty(addResult.Diagnostics);
        Assert.Equal(2, selectedSlotIndex);

        MainWindow.CompendiumDraftState draft = new(
            2,
            0x1111,
            "draft-xp",
            12,
            13,
            14,
            15,
            16,
            17,
            0x2101,
            0x2102,
            0x2103,
            0x2104,
            0x2105,
            0x2106,
            0x2107,
            0x2108);

        ushort selectedPersonaId = 3;
        string experienceText = "0";
        double level = 1;
        double strength = 1;
        double magic = 1;
        double endurance = 1;
        double agility = 1;
        double luck = 1;
        ushort[] skillIds = [0, 0, 0, 0, 0, 0, 0, 0];

        bool preserveSelectedCompendiumDraft = MainWindow.ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(
            2,
            selectedSlotIndex,
            addResult.Succeeded,
            compendiumSlots[2].PersonaId == selectedChoice.PersonaId);

        Assert.False(preserveSelectedCompendiumDraft);

        SimulateCompendiumDraftRefresh(
            draft,
            selectedSlotIndex,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds,
            preserveSelectedCompendiumDraft);

        Assert.Equal((ushort)3, selectedPersonaId);
        Assert.Equal("0", experienceText);
        Assert.Equal(1d, level);
        Assert.Equal(1d, strength);
        Assert.Equal(1d, magic);
        Assert.Equal(1d, endurance);
        Assert.Equal(1d, agility);
        Assert.Equal(1d, luck);
        Assert.Equal(new[] { (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0 }, skillIds);
    }

    [Fact]
    public void CompendiumAddRefreshPreservesDraftWhenFullAddFailureLeavesSelectionStable()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, true, 0x2222, 13, 101, [1, 2, 3, 4, 5, 6, 7, 8], 11, 12, 13, 14, 15),
        ];

        PersonaChoiceViewState selectedChoice = new(0xBEEF, "Missing Persona");
        int selectedSlotIndex = 0;
        int editCallCount = 0;

        SaveEditorOperationResult addResult = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) =>
            {
                editCallCount++;
                return new SaveEditorOperationResult(false, []);
            },
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.False(addResult.Succeeded);
        Assert.Single(addResult.Diagnostics);
        Assert.Equal("P4GWINUI027", addResult.Diagnostics[0].Code);
        Assert.Equal(0, selectedSlotIndex);
        Assert.Equal(0, editCallCount);

        MainWindow.CompendiumDraftState draft = new(
            0,
            0x1111,
            "draft-xp",
            12,
            13,
            14,
            15,
            16,
            17,
            0x2101,
            0x2102,
            0x2103,
            0x2104,
            0x2105,
            0x2106,
            0x2107,
            0x2108);

        ushort selectedPersonaId = 0x2222;
        string experienceText = "committed";
        double level = 22;
        double strength = 23;
        double magic = 24;
        double endurance = 25;
        double agility = 26;
        double luck = 27;
        ushort[] skillIds = [9, 10, 11, 12, 13, 14, 15, 16];

        SimulateCompendiumDraftRefresh(
            draft,
            selectedSlotIndex,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds);

        Assert.Equal(draft.PersonaId, selectedPersonaId);
        Assert.Equal(draft.ExperienceText, experienceText);
        Assert.Equal(draft.Level, level);
        Assert.Equal(draft.Strength, strength);
        Assert.Equal(draft.Magic, magic);
        Assert.Equal(draft.Endurance, endurance);
        Assert.Equal(draft.Agility, agility);
        Assert.Equal(draft.Luck, luck);
        Assert.Equal(new[] { draft.Skill1Id, draft.Skill2Id, draft.Skill3Id, draft.Skill4Id, draft.Skill5Id, draft.Skill6Id, draft.Skill7Id, draft.Skill8Id }, skillIds);
    }

    [Fact]
    public void CompendiumAddRefreshDoesNotRestoreDraftWhenSelectionMovesToDifferentSlot()
    {
        IReadOnlyList<PersonaSlotViewState> compendiumSlots =
        [
            new(0, true, 0x1111, 12, 100, [1, 2, 3, 4, 5, 6, 7, 8], 10, 11, 12, 13, 14),
            new(1, false, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0),
        ];

        PersonaChoiceViewState selectedChoice = new(0xBEEF, "New Persona");
        int selectedSlotIndex = 0;

        SaveEditorOperationResult addResult = MainWindow.SelectOrAddCompendiumPersonaCore(
            compendiumSlots,
            selectedChoice,
            (_, _) => new SaveEditorOperationResult(true, []),
            slotIndex => selectedSlotIndex = slotIndex);

        Assert.True(addResult.Succeeded);
        Assert.Equal(1, selectedSlotIndex);

        MainWindow.CompendiumDraftState draft = new(
            0,
            0x1111,
            "draft-a",
            12,
            13,
            14,
            15,
            16,
            17,
            0x2101,
            0x2102,
            0x2103,
            0x2104,
            0x2105,
            0x2106,
            0x2107,
            0x2108);

        ushort selectedPersonaId = 0x2222;
        string experienceText = "model-b";
        double level = 22;
        double strength = 23;
        double magic = 24;
        double endurance = 25;
        double agility = 26;
        double luck = 27;
        ushort[] skillIds = [9, 10, 11, 12, 13, 14, 15, 16];

        SimulateCompendiumDraftRefresh(
            draft,
            selectedSlotIndex,
            ref selectedPersonaId,
            ref experienceText,
            ref level,
            ref strength,
            ref magic,
            ref endurance,
            ref agility,
            ref luck,
            skillIds);

        Assert.Equal((ushort)0x2222, selectedPersonaId);
        Assert.Equal("model-b", experienceText);
        Assert.Equal(22d, level);
        Assert.Equal(23d, strength);
        Assert.Equal(24d, magic);
        Assert.Equal(25d, endurance);
        Assert.Equal(26d, agility);
        Assert.Equal(27d, luck);
        Assert.Equal(new[] { (ushort)9, (ushort)10, (ushort)11, (ushort)12, (ushort)13, (ushort)14, (ushort)15, (ushort)16 }, skillIds);
    }

    [Fact]
    public void CompendiumDraftAfterApplyIsPreservedOnlyWhenBatchDoesNotTouchCompendium()
    {
        Assert.True(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterApply(Array.Empty<SaveEditCommand>()));

        Assert.True(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterApply(
            [new SetYenEdit(123), new SetSaveNamesEdit("Family", "Given")]));

        Assert.False(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterApply(
            [
                new SetYenEdit(123),
                new SetCompendiumPersonaSlotEdit(2, new PersonaSlotEdit(0x0404, 18, 0x04040404, [0x4401, 0x4402, 0x4403, 0x4404, 0x4405, 0x4406, 0x4407, 0x4408], 44, 55, 66, 77, 88)),
            ]));

        Assert.False(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterApply(
            [new SetCompendiumPersonaSlotEdit(2, new PersonaSlotEdit(0x0404, 18, 0x04040404, [0x4401, 0x4402, 0x4403, 0x4404, 0x4405, 0x4406, 0x4407, 0x4408], 44, 55, 66, 77, 88))]));

        Assert.False(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterApply(
            [new ClearCompendiumPersonaSlotEdit(2)]));

        Assert.False(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterApply(
            [new ClearCompendiumPersonaSlotsEdit()]));
    }

    [Fact]
    public void CompendiumDraftAfterSelectOrAddIsPreservedOnlyWhenSelectionContextIsStableOrMutationFails()
    {
        Assert.True(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(0, 0, true, true));
        Assert.False(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(0, 0, true, false));
        Assert.False(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(0, 1, true, true));
        Assert.True(MainWindow.ShouldPreserveSelectedCompendiumDraftAfterSelectOrAdd(0, 1, false, false));
    }

    [Fact]
    public void CompendiumMutationRefreshHelperClearsSelectionOnlyAfterSuccessfulMutation()
    {
        int? selectedCompendiumSlotIndex = 3;
        bool clearCalled = false;
        bool preserveSelectedCompendiumDraft = true;

        SaveEditorOperationResult result = MainWindow.RefreshCompendiumDraftPreservingSelection(
            () => new SaveEditorOperationResult(true, []),
            preserveSelectedCompendiumDraftValue => preserveSelectedCompendiumDraft = preserveSelectedCompendiumDraftValue,
            () =>
            {
                clearCalled = true;
                selectedCompendiumSlotIndex = null;
            });

        Assert.True(result.Succeeded);
        Assert.True(clearCalled);
        Assert.False(preserveSelectedCompendiumDraft);
        Assert.Null(selectedCompendiumSlotIndex);
    }

    [Fact]
    public void CompendiumMutationRefreshHelperClearsSelectionBeforeRefreshCallbackCanRestoreDraft()
    {
        int? selectedCompendiumSlotIndex = 3;
        bool clearCalled = false;
        bool refreshCallbackObservedClearedSelection = false;

        MainWindow.CompendiumDraftState draft = new(
            3,
            0x0404,
            "draft-xp",
            12,
            13,
            14,
            15,
            16,
            17,
            0x2101,
            0x2102,
            0x2103,
            0x2104,
            0x2105,
            0x2106,
            0x2107,
            0x2108);

        ushort selectedPersonaId = 0x2222;
        string experienceText = "committed";
        double level = 22;
        double strength = 23;
        double magic = 24;
        double endurance = 25;
        double agility = 26;
        double luck = 27;
        ushort[] skillIds = [9, 10, 11, 12, 13, 14, 15, 16];

        SaveEditorOperationResult result = MainWindow.RefreshCompendiumDraftPreservingSelection(
            () => new SaveEditorOperationResult(true, []),
            _ =>
            {
                refreshCallbackObservedClearedSelection = selectedCompendiumSlotIndex is null;
                Assert.Null(selectedCompendiumSlotIndex);
                SimulateCompendiumDraftRefresh(
                    draft,
                    selectedCompendiumSlotIndex,
                    ref selectedPersonaId,
                    ref experienceText,
                    ref level,
                    ref strength,
                    ref magic,
                    ref endurance,
                    ref agility,
                    ref luck,
                    skillIds);
            },
            () =>
            {
                clearCalled = true;
                selectedCompendiumSlotIndex = null;
            });

        Assert.True(result.Succeeded);
        Assert.True(clearCalled);
        Assert.True(refreshCallbackObservedClearedSelection);
        Assert.Null(selectedCompendiumSlotIndex);
        Assert.Equal((ushort)0x2222, selectedPersonaId);
        Assert.Equal("committed", experienceText);
        Assert.Equal(22d, level);
        Assert.Equal(23d, strength);
        Assert.Equal(24d, magic);
        Assert.Equal(25d, endurance);
        Assert.Equal(26d, agility);
        Assert.Equal(27d, luck);
        Assert.Equal(new[] { (ushort)9, (ushort)10, (ushort)11, (ushort)12, (ushort)13, (ushort)14, (ushort)15, (ushort)16 }, skillIds);
    }

    [Fact]
    public void CompendiumMutationRefreshHelperPreservesSelectionAndDraftWhenMutationFails()
    {
        int? selectedCompendiumSlotIndex = 3;
        bool clearCalled = false;
        bool preserveSelectedCompendiumDraft = false;

        SaveEditorOperationResult result = MainWindow.RefreshCompendiumDraftPreservingSelection(
            () => new SaveEditorOperationResult(
                false,
                [new SaveDiagnostic(DiagnosticSeverity.Error, "P4GWINUI026", "Select a compendium entry before removing it.", "Compendium.Item")]),
            preserveSelectedCompendiumDraftValue => preserveSelectedCompendiumDraft = preserveSelectedCompendiumDraftValue,
            () =>
            {
                clearCalled = true;
                selectedCompendiumSlotIndex = null;
            });

        Assert.False(result.Succeeded);
        Assert.False(clearCalled);
        Assert.True(preserveSelectedCompendiumDraft);
        Assert.Equal(3, selectedCompendiumSlotIndex);
    }

    private static void SimulateCompendiumDraftRefresh(
        MainWindow.CompendiumDraftState draft,
        int? selectedCompendiumSlotIndexAfterRefresh,
        ref ushort selectedPersonaId,
        ref string experienceText,
        ref double level,
        ref double strength,
        ref double magic,
        ref double endurance,
        ref double agility,
        ref double luck,
        ushort[] skillIds,
        bool preserveSelectedCompendiumDraft = true)
    {
        if (!preserveSelectedCompendiumDraft ||
            !MainWindow.ShouldRestoreSelectedCompendiumDraft(draft, selectedCompendiumSlotIndexAfterRefresh))
        {
            return;
        }

        selectedPersonaId = draft.PersonaId;
        experienceText = draft.ExperienceText;
        level = draft.Level;
        strength = draft.Strength;
        magic = draft.Magic;
        endurance = draft.Endurance;
        agility = draft.Agility;
        luck = draft.Luck;
        skillIds[0] = draft.Skill1Id;
        skillIds[1] = draft.Skill2Id;
        skillIds[2] = draft.Skill3Id;
        skillIds[3] = draft.Skill4Id;
        skillIds[4] = draft.Skill5Id;
        skillIds[5] = draft.Skill6Id;
        skillIds[6] = draft.Skill7Id;
        skillIds[7] = draft.Skill8Id;
    }
}
