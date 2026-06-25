namespace P4G.SaveTool.Contracts;

public static class CalendarPhaseRules
{
    public const int PhaseCount = 6;

    public static bool IsSupportedPhaseId(int phaseId) =>
        (uint)phaseId < PhaseCount;
}
