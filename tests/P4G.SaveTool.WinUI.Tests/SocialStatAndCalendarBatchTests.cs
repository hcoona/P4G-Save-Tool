using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using P4G.SaveTool.Contracts;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class SocialStatAndCalendarBatchTests
{
    [Fact]
    public void Group4EditInputsFactoryMapsSelectionsToExpectedSlots()
    {
        SocialStatRankChoiceViewState courageRank = new(10, "Courage Rank");
        SocialStatRankChoiceViewState knowledgeRank = new(11, "Knowledge Rank");
        SocialStatRankChoiceViewState expressionRank = new(12, "Expression Rank");
        SocialStatRankChoiceViewState understandingRank = new(13, "Understanding Rank");
        SocialStatRankChoiceViewState diligenceRank = new(14, "Diligence Rank");
        CalendarPhaseChoiceViewState dayPhase = new(20, "Day Phase", true);
        CalendarPhaseChoiceViewState nextPhase = new(21, "Next Phase", false);

        Group4EditInputs inputs = MainWindow.CreateGroup4EditInputs(
            courageRank,
            knowledgeRank,
            expressionRank,
            understandingRank,
            diligenceRank,
            "14",
            dayPhase,
            "15",
            nextPhase);

        Assert.Same(courageRank, inputs.CourageRank);
        Assert.Same(knowledgeRank, inputs.KnowledgeRank);
        Assert.Same(expressionRank, inputs.ExpressionRank);
        Assert.Same(understandingRank, inputs.UnderstandingRank);
        Assert.Same(diligenceRank, inputs.DiligenceRank);
        Assert.Equal("14", inputs.DayText);
        Assert.Same(dayPhase, inputs.DayPhase);
        Assert.Equal("15", inputs.NextDayText);
        Assert.Same(nextPhase, inputs.NextPhase);
    }

    [Fact]
    public void MergeGroup4BatchResultsAppendsGroup4EditsAndDiagnostics()
    {
        List<SaveEditCommand> batch =
        [
            new SetYenEdit(123u),
        ];
        List<SaveDiagnostic> diagnostics =
        [
            new(DiagnosticSeverity.Warning, "W1", "Existing warning.", "Yen"),
        ];
        IReadOnlyList<SaveEditCommand> group4Edits =
        [
            new SetSocialStatRankEdit(2, 5),
            new SetNextDayEdit(14),
        ];
        IReadOnlyList<SaveDiagnostic> group4Diagnostics =
        [
            new SaveDiagnostic(DiagnosticSeverity.Error, "G4", "Group 4 diagnostic.", "SocialStats.Diligence"),
        ];

        MainWindow.MergeGroup4BatchResults(batch, diagnostics, group4Edits, group4Diagnostics);

        Assert.Equal(
            [
                new SetYenEdit(123u),
                new SetSocialStatRankEdit(2, 5),
                new SetNextDayEdit(14),
            ],
            batch);
        Assert.Equal(
            [
                new SaveDiagnostic(DiagnosticSeverity.Warning, "W1", "Existing warning.", "Yen"),
                new SaveDiagnostic(DiagnosticSeverity.Error, "G4", "Group 4 diagnostic.", "SocialStats.Diligence"),
            ],
            diagnostics);
    }

    [Fact]
    public void AppendGroup4EditsPreservesExistingBatchAndPassesThroughInputs()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();
        List<SaveEditCommand> batch =
        [
            new SetYenEdit(123u),
            new SetSaveNamesEdit("Amagi", "Chie"),
        ];
        List<SaveDiagnostic> diagnostics = [];

        MainWindow.AppendGroup4Edits(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "14",
                new(8, "Evening"),
                "15",
                new(9, "Late Night")),
            batch,
            diagnostics);

        Assert.Equal(
            [
                new SetYenEdit(123u),
                new SetSaveNamesEdit("Amagi", "Chie"),
                new SetSocialStatRankEdit(0, 2),
                new SetSocialStatRankEdit(1, 3),
                new SetSocialStatRankEdit(4, 4),
                new SetSocialStatRankEdit(3, 5),
                new SetSocialStatRankEdit(2, 6),
                new SetDayEdit(14),
                new SetDayPhaseEdit(8),
                new SetNextDayEdit(15),
                new SetNextDayPhaseEdit(9),
            ],
            batch);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void TryFinalizeEditBatchSuppressesEditsWhenGroup4ValidationFails()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();
        List<SaveEditCommand> batch =
        [
            new SetYenEdit(123u),
            new SetSaveNamesEdit("Amagi", "Chie"),
        ];
        List<SaveDiagnostic> diagnostics = [];

        MainWindow.AppendGroup4Edits(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "not-a-number",
                new(8, "Evening"),
                "15",
                new(9, "Late Night")),
            batch,
            diagnostics);

        bool succeeded = MainWindow.TryFinalizeEditBatch(batch, diagnostics, out IReadOnlyList<SaveEditCommand> edits, out IReadOnlyList<SaveDiagnostic> finalDiagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Equal(diagnostics, finalDiagnostics);
        Assert.Equal("P4GWINUI018", finalDiagnostics[0].Code);
        Assert.Equal(
            [
                new SetYenEdit(123u),
                new SetSaveNamesEdit("Amagi", "Chie"),
            ],
            batch);
    }

    [Fact]
    public void TryFinalizeEditBatchPreservesBatchWhenValidationSucceeds()
    {
        List<SaveEditCommand> batch =
        [
            new SetYenEdit(123u),
            new SetSaveNamesEdit("Amagi", "Chie"),
        ];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryFinalizeEditBatch(batch, diagnostics, out IReadOnlyList<SaveEditCommand> edits, out IReadOnlyList<SaveDiagnostic> finalDiagnostics);

        Assert.True(succeeded);
        Assert.Same(batch, edits);
        Assert.Empty(finalDiagnostics);
        Assert.Equal(batch, edits);
    }

    [Fact]
    public void UnchangedMidRankSocialStatSelectionsAreSkipped()
    {
        SocialStatViewState currentStat = new(0, "Courage", 18, 2, "Reliable");
        SocialStatRankChoiceViewState selectedRank = new(2, "Reliable");

        Assert.True(MainWindow.ShouldSkipSocialStatEdit(currentStat, selectedRank));
    }

    [Fact]
    public void UnchangedUnknownCalendarPhaseSelectionsAreSkipped()
    {
        CalendarPhaseChoiceViewState selectedPhase = new(9, "Unknown phase (9)", true);

        Assert.True(MainWindow.ShouldSkipCalendarPhaseEdit(9, selectedPhase));
    }

    [Fact]
    public void Group4BatchIncludesAllChangedSocialStatsAndCalendarEdits()
    {
        IReadOnlyList<SocialStatViewState> socialStats =
        [
            new(0, "Courage", 1, 1, "Awful"),
            new(1, "Knowledge", 2, 2, "Bad"),
            new(2, "Diligence", 5, 5, "Great"),
            new(3, "Understanding", 4, 4, "Good"),
            new(4, "Expression", 3, 3, "Okay"),
        ];
        CalendarViewState calendar = new(12, 6, 13, 7);

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "14",
                new(8, "Evening"),
                "15",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.True(succeeded);
        Assert.Empty(diagnostics);
        Assert.Equal(
            [
                new SetSocialStatRankEdit(0, 2),
                new SetSocialStatRankEdit(1, 3),
                new SetSocialStatRankEdit(4, 4),
                new SetSocialStatRankEdit(3, 5),
                new SetSocialStatRankEdit(2, 6),
                new SetDayEdit(14),
                new SetDayPhaseEdit(8),
                new SetNextDayEdit(15),
                new SetNextDayPhaseEdit(9),
            ],
            edits);
    }

    [Fact]
    public void Group4BatchOmitsUnchangedSocialStatSelections()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(1, "Awful"),
                new(8, "Changed Knowledge"),
                new(3, "Okay"),
                new(9, "Changed Understanding"),
                new(5, "Great"),
                "14",
                new(8, "Evening"),
                "15",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.True(succeeded);
        Assert.Empty(diagnostics);
        Assert.Equal(
            [
                new SetSocialStatRankEdit(1, 8),
                new SetSocialStatRankEdit(3, 9),
                new SetDayEdit(14),
                new SetDayPhaseEdit(8),
                new SetNextDayEdit(15),
                new SetNextDayPhaseEdit(9),
            ],
            edits);
    }

    [Fact]
    public void Group4BatchOmitsUnchangedCalendarPhaseSelections()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "14",
                new(6, "Day phase"),
                "15",
                new(7, "Next phase")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.True(succeeded);
        Assert.Empty(diagnostics);
        Assert.Equal(
            [
                new SetSocialStatRankEdit(0, 2),
                new SetSocialStatRankEdit(1, 3),
                new SetSocialStatRankEdit(4, 4),
                new SetSocialStatRankEdit(3, 5),
                new SetSocialStatRankEdit(2, 6),
                new SetDayEdit(14),
                new SetNextDayEdit(15),
            ],
            edits);
    }

    [Fact]
    public void InvalidGroup4DayInputPreventsBatchConstruction()
    {
        IReadOnlyList<SocialStatViewState> socialStats =
        [
            new(0, "Courage", 1, 1, "Awful"),
            new(1, "Knowledge", 2, 2, "Bad"),
            new(4, "Expression", 3, 3, "Okay"),
            new(3, "Understanding", 4, 4, "Good"),
            new(2, "Diligence", 5, 5, "Great"),
        ];
        CalendarViewState calendar = new(12, 6, 13, 7);

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "not-a-number",
                new(8, "Evening"),
                "15",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Single(diagnostics);
        Assert.Equal("P4GWINUI018", diagnostics[0].Code);
        Assert.Equal("Calendar.Day", diagnostics[0].Target);
    }

    [Fact]
    public void InvalidGroup4InputsAccumulateDiagnosticsAndReturnNoEdits()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                null,
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "not-a-number",
                null,
                "15",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Equal(
            [
                new SaveDiagnostic(DiagnosticSeverity.Error, "P4GWINUI017", "Select a rank for Courage.", "SocialStats.Courage"),
                new SaveDiagnostic(DiagnosticSeverity.Error, "P4GWINUI018", "Day must be a whole number.", "Calendar.Day"),
                new SaveDiagnostic(DiagnosticSeverity.Error, "P4GWINUI019", "Select a phase for the current day.", "Calendar.Phase"),
            ],
            diagnostics);
    }

    [Theory]
    [InlineData(0, "SocialStats.Courage")]
    [InlineData(1, "SocialStats.Knowledge")]
    [InlineData(2, "SocialStats.Expression")]
    [InlineData(3, "SocialStats.Understanding")]
    [InlineData(4, "SocialStats.Diligence")]
    public void MissingSocialStatSelectionReturnsP4GWINUI017(int missingSelectionIndex, string expectedTarget)
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();
        SocialStatRankChoiceViewState?[] ranks =
        [
            new(2, "Reliable"),
            new(3, "Sharp"),
            new(4, "Smooth"),
            new(5, "Clear"),
            new(6, "Steady"),
        ];
        ranks[missingSelectionIndex] = null;

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                ranks[0],
                ranks[1],
                ranks[2],
                ranks[3],
                ranks[4],
                "14",
                new(8, "Evening"),
                "15",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Single(diagnostics);
        Assert.Equal("P4GWINUI017", diagnostics[0].Code);
        Assert.Equal(expectedTarget, diagnostics[0].Target);
    }

    [Fact]
    public void MissingCurrentPhaseSelectionReturnsP4GWINUI019()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "14",
                null,
                "15",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Single(diagnostics);
        Assert.Equal("P4GWINUI019", diagnostics[0].Code);
        Assert.Equal("Calendar.Phase", diagnostics[0].Target);
    }

    [Fact]
    public void InvalidNextDayInputReturnsP4GWINUI020()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "14",
                new(8, "Evening"),
                "not-a-number",
                new(9, "Late Night")),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Single(diagnostics);
        Assert.Equal("P4GWINUI020", diagnostics[0].Code);
        Assert.Equal("Calendar.NextDay", diagnostics[0].Target);
    }

    [Fact]
    public void MissingNextPhaseSelectionReturnsP4GWINUI021()
    {
        (IReadOnlyList<SocialStatViewState> socialStats, CalendarViewState calendar) = CreateGroup4BatchState();

        bool succeeded = Group4EditBatchBuilder.TryBuild(
            socialStats,
            calendar,
            MainWindow.CreateGroup4EditInputs(
                new(2, "Reliable"),
                new(3, "Sharp"),
                new(4, "Smooth"),
                new(5, "Clear"),
                new(6, "Steady"),
                "14",
                new(8, "Evening"),
                "15",
                null),
            out IReadOnlyList<SaveEditCommand> edits,
            out IReadOnlyList<SaveDiagnostic> diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Single(diagnostics);
        Assert.Equal("P4GWINUI021", diagnostics[0].Code);
        Assert.Equal("Calendar.NextPhase", diagnostics[0].Target);
    }

    private static (IReadOnlyList<SocialStatViewState> SocialStats, CalendarViewState Calendar) CreateGroup4BatchState() =>
        (
            [
                new(0, "Courage", 1, 1, "Awful"),
                new(1, "Knowledge", 2, 2, "Bad"),
                new(2, "Diligence", 5, 5, "Great"),
                new(3, "Understanding", 4, 4, "Good"),
                new(4, "Expression", 3, 3, "Okay"),
            ],
            new(12, 6, 13, 7));
}
