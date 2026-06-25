namespace P4G.SaveTool.Contracts;

public static class SocialStatRules
{
    public const int StatCount = 5;
    public const int MinimumRank = 1;
    public const int MaximumRank = 5;

    public static bool IsSupportedStatIndex(int statIndex) =>
        (uint)statIndex < StatCount;

    public static bool IsSupportedRank(int rank) =>
        rank is >= MinimumRank and <= MaximumRank;

    public static byte PointsToRank(int statIndex, ushort points) =>
        statIndex switch
        {
            0 or 2 or 3 => PointsToStandardRank(points),
            1 => PointsToKnowledgeRank(points),
            4 => PointsToExpressionRank(points),
            _ => throw new ArgumentOutOfRangeException(nameof(statIndex), statIndex, "Unknown social stat index."),
        };

    public static ushort RankToPoints(int statIndex, int rank) =>
        statIndex switch
        {
            0 or 2 or 3 => RankToStandardPoints(rank),
            1 => RankToKnowledgePoints(rank),
            4 => RankToExpressionPoints(rank),
            _ => throw new ArgumentOutOfRangeException(nameof(statIndex), statIndex, "Unknown social stat index."),
        };

    private static byte PointsToStandardRank(ushort points) =>
        points switch
        {
            <= 15 => 1,
            <= 39 => 2,
            <= 79 => 3,
            <= 139 => 4,
            _ => 5,
        };

    private static byte PointsToKnowledgeRank(ushort points) =>
        points switch
        {
            <= 29 => 1,
            <= 79 => 2,
            <= 149 => 3,
            <= 239 => 4,
            _ => 5,
        };

    private static byte PointsToExpressionRank(ushort points) =>
        points switch
        {
            <= 12 => 1,
            <= 32 => 2,
            <= 52 => 3,
            <= 84 => 4,
            _ => 5,
        };

    private static ushort RankToStandardPoints(int rank) =>
        rank switch
        {
            1 => 15,
            2 => 16,
            3 => 40,
            4 => 80,
            5 => 140,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Social stat rank is out of range."),
        };

    private static ushort RankToKnowledgePoints(int rank) =>
        rank switch
        {
            1 => 29,
            2 => 30,
            3 => 80,
            4 => 150,
            5 => 240,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Social stat rank is out of range."),
        };

    private static ushort RankToExpressionPoints(int rank) =>
        rank switch
        {
            1 => 12,
            2 => 13,
            3 => 33,
            4 => 53,
            5 => 85,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Social stat rank is out of range."),
        };
}
