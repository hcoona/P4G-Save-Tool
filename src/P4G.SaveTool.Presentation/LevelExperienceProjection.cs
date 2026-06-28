namespace P4G.SaveTool.Presentation;

public static class LevelExperienceProjection
{
    public static uint CalculateTotalExperienceFromLevel(byte level)
    {
        ulong value = level;
        return (uint)((value * value * value * value + 4 * value * value * value + 53 * value * value - 58 * value) / 10);
    }
}
