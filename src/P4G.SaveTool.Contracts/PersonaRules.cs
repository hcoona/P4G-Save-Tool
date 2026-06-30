namespace P4G.SaveTool.Contracts;

public static class PersonaRules
{
    private static readonly (ushort Start, ushort End)[] PersonaRanges =
    [
        (1, 42),
        (44, 51),
        (53, 179),
        (182, 213),
        (224, 249),
    ];

    public static bool IsSupportedPersonaId(ushort personaId) => IsInRanges(personaId, PersonaRanges);

    public static IEnumerable<ushort> EnumerateSupportedPersonaIds()
    {
        foreach ((ushort start, ushort end) in PersonaRanges)
        {
            for (ushort personaId = start; personaId <= end; personaId++)
            {
                yield return personaId;
            }
        }
    }

    private static bool IsInRanges(ushort personaId, ReadOnlySpan<(ushort Start, ushort End)> ranges)
    {
        foreach ((ushort start, ushort end) in ranges)
        {
            if (personaId >= start && personaId <= end)
            {
                return true;
            }
        }

        return false;
    }
}
