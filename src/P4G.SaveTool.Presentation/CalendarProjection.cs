using System.Collections.ObjectModel;
using System.Linq;
using P4G.SaveTool.Catalog;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Presentation;

internal static class CalendarProjection
{
    private static readonly ReadOnlyCollection<CalendarPhaseChoiceViewState> phaseChoices =
        Array.AsReadOnly(P4GCatalog.CalendarPhases.Select(static phase => new CalendarPhaseChoiceViewState(phase.Id, phase.Name)).ToArray());

    internal static IReadOnlyList<CalendarPhaseChoiceViewState> PhaseChoices => phaseChoices;

    internal static CalendarViewState ProjectCalendar(WorkingSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new CalendarViewState(state.Day, state.DayPhase, state.NextDay, state.NextDayPhase);
    }

    internal static IReadOnlyList<CalendarPhaseChoiceViewState> GetPhaseChoices(int currentPhaseId, out CalendarPhaseChoiceViewState selectedChoice)
    {
        if (CalendarPhaseRules.IsSupportedPhaseId(currentPhaseId))
        {
            selectedChoice = phaseChoices[currentPhaseId];
            return phaseChoices;
        }

        selectedChoice = new CalendarPhaseChoiceViewState(currentPhaseId, $"Unknown phase ({currentPhaseId})", true);
        List<CalendarPhaseChoiceViewState> choices = new(phaseChoices.Count + 1);
        choices.AddRange(phaseChoices);
        choices.Add(selectedChoice);
        return Array.AsReadOnly(choices.ToArray());
    }
}
