using System.Collections.ObjectModel;
using System.Linq;
using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.Presentation;

internal static class SocialStatProjection
{
    private static readonly SocialStatDefinition[] Definitions =
    [
        new(0, "Courage", ["Average", "Reliable", "Brave", "Daring", "Heroic"]),
        new(1, "Knowledge", ["Aware", "Informed", "Expert", "Professor", "Sage"]),
        new(2, "Diligence", ["Callow", "Persistent", "Strong", "Thorough", "Rock Solid"]),
        new(3, "Understanding", ["Basic", "Kindly", "Generous", "Motherly", "Saintly"]),
        new(4, "Expression", ["Rough", "Eloquent", "Persuasive", "Touching", "Enthralling"]),
    ];

    private static readonly ReadOnlyCollection<SocialStatRankChoiceViewState>[] RankChoices = CreateRankChoices();

    internal static ReadOnlyCollection<SocialStatViewState> ProjectSocialStats(IReadOnlyList<ushort> socialStats)
    {
        ArgumentNullException.ThrowIfNull(socialStats);
        if (socialStats.Count != Definitions.Length)
        {
            throw new ArgumentException($"Social stats must contain exactly {Definitions.Length} values.", nameof(socialStats));
        }

        SocialStatViewState[] stats = new SocialStatViewState[Definitions.Length];
        for (int index = 0; index < Definitions.Length; index++)
        {
            stats[index] = ProjectSocialStat(index, socialStats[index]);
        }

        return Array.AsReadOnly(stats);
    }

    internal static IReadOnlyList<SocialStatRankChoiceViewState> GetRankChoices(int statIndex, ushort currentPoints, out SocialStatRankChoiceViewState selectedChoice)
    {
        if (!SocialStatRules.IsSupportedStatIndex(statIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(statIndex), statIndex, "Unknown social stat index.");
        }

        ReadOnlyCollection<SocialStatRankChoiceViewState> choices = RankChoices[statIndex];
        int rank = SocialStatRules.PointsToRank(statIndex, currentPoints);
        selectedChoice = choices[rank - 1];
        return choices;
    }

    internal static SocialStatViewState ProjectSocialStat(int statIndex, ushort points)
    {
        if (!SocialStatRules.IsSupportedStatIndex(statIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(statIndex), statIndex, "Unknown social stat index.");
        }

        SocialStatDefinition definition = Definitions[statIndex];
        int rank = SocialStatRules.PointsToRank(statIndex, points);
        return new SocialStatViewState(statIndex, definition.Name, points, rank, definition.RankNames[rank - 1]);
    }

    private static ReadOnlyCollection<SocialStatRankChoiceViewState>[] CreateRankChoices() =>
        Definitions
            .Select(static definition => Array.AsReadOnly(
                definition.RankNames
                    .Select((rankName, index) => new SocialStatRankChoiceViewState(index + 1, rankName))
                    .ToArray()))
            .ToArray();

    private sealed record SocialStatDefinition(int StatIndex, string Name, string[] RankNames);
}
