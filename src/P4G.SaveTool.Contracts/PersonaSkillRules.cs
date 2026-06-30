namespace P4G.SaveTool.Contracts;

public static class PersonaSkillRules
{
    private static readonly (ushort Start, ushort Length)[] LegacySkillRanges =
    [
        (0, 255),
        (259, 42),
        (349, 46),
        (440, 13),
        (472, 151),
    ];

    public static bool IsSupportedSkillId(ushort skillId)
    {
        foreach ((ushort start, ushort length) in LegacySkillRanges)
        {
            int end = start + length;
            if (skillId >= start && skillId < end)
            {
                return true;
            }
        }

        return false;
    }

    public static ushort NormalizeSkillId(ushort skillId) =>
        IsSupportedSkillId(skillId) ? skillId : (ushort)0;

    public static IEnumerable<ushort> EnumerateSupportedSkillIds()
    {
        foreach ((ushort start, ushort length) in LegacySkillRanges)
        {
            int end = start + length;
            for (ushort skillId = start; skillId < end; skillId++)
            {
                yield return skillId;
            }
        }
    }
}
