namespace P4G.SaveTool.Presentation;

public sealed record CalendarPhaseChoiceViewState(int PhaseId, string Name, bool IsUnknown = false)
{
    public override string ToString() => Name;
}
