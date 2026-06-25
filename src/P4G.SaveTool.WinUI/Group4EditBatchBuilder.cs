using System.Globalization;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

internal sealed record Group4EditInputs(
    SocialStatRankChoiceViewState? CourageRank,
    SocialStatRankChoiceViewState? KnowledgeRank,
    SocialStatRankChoiceViewState? ExpressionRank,
    SocialStatRankChoiceViewState? UnderstandingRank,
    SocialStatRankChoiceViewState? DiligenceRank,
    string DayText,
    CalendarPhaseChoiceViewState? DayPhase,
    string NextDayText,
    CalendarPhaseChoiceViewState? NextPhase);

internal static class Group4EditBatchBuilder
{
    internal static bool TryBuild(
        IReadOnlyList<SocialStatViewState> currentSocialStats,
        CalendarViewState currentCalendar,
        Group4EditInputs inputs,
        out IReadOnlyList<SaveEditCommand> edits,
        out IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(currentSocialStats);
        ArgumentNullException.ThrowIfNull(currentCalendar);
        ArgumentNullException.ThrowIfNull(inputs);

        List<SaveEditCommand> batch = [];
        List<SaveDiagnostic> validationDiagnostics = [];

        AddSocialStatEdit(currentSocialStats[0], 0, "Courage", inputs.CourageRank, batch, validationDiagnostics);
        AddSocialStatEdit(currentSocialStats[1], 1, "Knowledge", inputs.KnowledgeRank, batch, validationDiagnostics);
        AddSocialStatEdit(currentSocialStats[4], 4, "Expression", inputs.ExpressionRank, batch, validationDiagnostics);
        AddSocialStatEdit(currentSocialStats[3], 3, "Understanding", inputs.UnderstandingRank, batch, validationDiagnostics);
        AddSocialStatEdit(currentSocialStats[2], 2, "Diligence", inputs.DiligenceRank, batch, validationDiagnostics);
        AddCalendarDayEdit(currentCalendar.Day, inputs.DayText, false, batch, validationDiagnostics);
        AddCalendarPhaseEdit(currentCalendar.DayPhaseId, inputs.DayPhase, false, batch, validationDiagnostics);
        AddCalendarDayEdit(currentCalendar.NextDay, inputs.NextDayText, true, batch, validationDiagnostics);
        AddCalendarPhaseEdit(currentCalendar.NextDayPhaseId, inputs.NextPhase, true, batch, validationDiagnostics);

        edits = validationDiagnostics.Count == 0 ? batch : [];
        diagnostics = validationDiagnostics;
        return validationDiagnostics.Count == 0;
    }

    private static void AddSocialStatEdit(
        SocialStatViewState currentStat,
        int statIndex,
        string statName,
        SocialStatRankChoiceViewState? selectedRank,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (selectedRank is null)
        {
            diagnostics.Add(CreateDiagnostic(
                "P4GWINUI017",
                $"Select a rank for {statName}.",
                $"SocialStats.{statName}"));
            return;
        }

        if (MainWindow.ShouldSkipSocialStatEdit(currentStat, selectedRank))
        {
            return;
        }

        edits.Add(new SetSocialStatRankEdit(statIndex, selectedRank.Rank));
    }

    private static void AddCalendarDayEdit(
        int currentDay,
        string dayText,
        bool isNextDay,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (int.TryParse(dayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
        {
            edits.Add(isNextDay ? new SetNextDayEdit(day) : new SetDayEdit(day));
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            isNextDay ? "P4GWINUI020" : "P4GWINUI018",
            isNextDay ? "Next day must be a whole number." : "Day must be a whole number.",
            isNextDay ? "Calendar.NextDay" : "Calendar.Day"));
    }

    private static void AddCalendarPhaseEdit(
        int currentPhaseId,
        CalendarPhaseChoiceViewState? selectedPhase,
        bool isNextDay,
        List<SaveEditCommand> edits,
        List<SaveDiagnostic> diagnostics)
    {
        if (selectedPhase is null)
        {
            diagnostics.Add(CreateDiagnostic(
                isNextDay ? "P4GWINUI021" : "P4GWINUI019",
                isNextDay ? "Select a phase for the next day." : "Select a phase for the current day.",
                isNextDay ? "Calendar.NextPhase" : "Calendar.Phase"));
            return;
        }

        if (MainWindow.ShouldSkipCalendarPhaseEdit(currentPhaseId, selectedPhase))
        {
            return;
        }

        edits.Add(isNextDay
            ? new SetNextDayPhaseEdit(selectedPhase.PhaseId)
            : new SetDayPhaseEdit(selectedPhase.PhaseId));
    }

    private static SaveDiagnostic CreateDiagnostic(string code, string message, string target) =>
        new(DiagnosticSeverity.Error, code, message, target);
}
