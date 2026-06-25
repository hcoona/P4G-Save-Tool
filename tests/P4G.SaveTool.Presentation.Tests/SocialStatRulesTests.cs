using System;
using System.Collections.Generic;
using System.Linq;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.Presentation;
using Xunit;

namespace P4G.SaveTool.Presentation.Tests;

public sealed class SocialStatRulesTests
{
    public static TheoryData<int, ushort, byte, string> ProjectionBoundaryCases { get; } = new()
    {
        { 0, 15, 1, "Average" },
        { 0, 16, 2, "Reliable" },
        { 0, 39, 2, "Reliable" },
        { 0, 40, 3, "Brave" },
        { 0, 79, 3, "Brave" },
        { 0, 80, 4, "Daring" },
        { 0, 139, 4, "Daring" },
        { 0, 140, 5, "Heroic" },
        { 1, 29, 1, "Aware" },
        { 1, 30, 2, "Informed" },
        { 1, 79, 2, "Informed" },
        { 1, 80, 3, "Expert" },
        { 1, 149, 3, "Expert" },
        { 1, 150, 4, "Professor" },
        { 1, 239, 4, "Professor" },
        { 1, 240, 5, "Sage" },
        { 2, 15, 1, "Callow" },
        { 2, 16, 2, "Persistent" },
        { 2, 39, 2, "Persistent" },
        { 2, 40, 3, "Strong" },
        { 2, 79, 3, "Strong" },
        { 2, 80, 4, "Thorough" },
        { 2, 139, 4, "Thorough" },
        { 2, 140, 5, "Rock Solid" },
        { 3, 15, 1, "Basic" },
        { 3, 16, 2, "Kindly" },
        { 3, 39, 2, "Kindly" },
        { 3, 40, 3, "Generous" },
        { 3, 79, 3, "Generous" },
        { 3, 80, 4, "Motherly" },
        { 3, 139, 4, "Motherly" },
        { 3, 140, 5, "Saintly" },
        { 4, 12, 1, "Rough" },
        { 4, 13, 2, "Eloquent" },
        { 4, 32, 2, "Eloquent" },
        { 4, 33, 3, "Persuasive" },
        { 4, 52, 3, "Persuasive" },
        { 4, 53, 4, "Touching" },
        { 4, 84, 4, "Touching" },
        { 4, 85, 5, "Enthralling" },
    };

    public static TheoryData<int, int, ushort> RankToPointsBoundaryCases { get; } = new()
    {
        { 0, 1, 15 },
        { 0, 2, 16 },
        { 0, 3, 40 },
        { 0, 4, 80 },
        { 0, 5, 140 },
        { 1, 1, 29 },
        { 1, 2, 30 },
        { 1, 3, 80 },
        { 1, 4, 150 },
        { 1, 5, 240 },
        { 4, 1, 12 },
        { 4, 2, 13 },
        { 4, 3, 33 },
        { 4, 4, 53 },
        { 4, 5, 85 },
    };

    [Theory]
    [MemberData(nameof(ProjectionBoundaryCases))]
    public void OpenSaveProjectsBoundarySocialStatRanks(int statIndex, ushort points, byte expectedRank, string expectedRankName)
    {
        ushort[] socialStats = CreateSocialStats(statIndex, points);
        FakeSaveApplicationService service = new()
        {
            OpenHandler = _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(socialStats)),
                []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded);
        SocialStatViewState projectedStat = viewModel.SocialStats[statIndex];
        Assert.Equal(statIndex, projectedStat.StatIndex);
        Assert.Equal(points, projectedStat.Points);
        Assert.Equal(expectedRank, projectedStat.Rank);
        Assert.Equal(expectedRankName, projectedStat.RankName);

        IReadOnlyList<SocialStatRankChoiceViewState> rankChoices = viewModel.GetSocialStatChoices(
            statIndex,
            points,
            out SocialStatRankChoiceViewState selectedRank);

        Assert.Equal(5, rankChoices.Count);
        Assert.Same(rankChoices[expectedRank - 1], selectedRank);
        Assert.Equal(expectedRank, selectedRank.Rank);
        Assert.Equal(expectedRankName, selectedRank.Name);
    }

    [Theory]
    [MemberData(nameof(RankToPointsBoundaryCases))]
    public void RankToPointsReturnsBoundaryCutoffValues(int statIndex, int rank, ushort expectedPoints)
    {
        ushort points = SocialStatRules.RankToPoints(statIndex, rank);

        Assert.Equal(expectedPoints, points);
        Assert.Equal(rank, SocialStatRules.PointsToRank(statIndex, points));
    }

    private static ushort[] CreateSocialStats(int statIndex, ushort points)
    {
        ushort[] socialStats = [15, 30, 80, 140, 85];
        socialStats[statIndex] = points;
        return socialStats;
    }

    private static WorkingSaveState CreateState(IReadOnlyList<ushort>? socialStats = null) =>
        new(
            new SaveNames("Sato", "Yu"),
            123456u,
            [new PartyMemberId(0x01), new PartyMemberId(0xfe), new PartyMemberId(0x80)],
            [1, 39, 112, 150, 183, 217, 2305, 2434],
            [256, 266, 287, 293, 307, 315, 328, 334],
            [512, 615, 685, 687, 754, 512, 615, 754],
            [1792, 2040, 1792, 2040, 1792, 2040, 1792, 2040],
            [CreatePersonaSlot(0x0101, 77, 0x01010101, 0x1101)],
            [CreatePersonaSlot(0x0202, 44, 0x02020202, 0x2201)],
            [CreatePersonaSlot(0x0303, 22, 0x03030303, 0x3301)],
            [],
            socialStats ?? [15, 30, 80, 140, 85],
            18,
            4,
            19,
            5);

    private static PersonaSlot CreatePersonaSlot(
        ushort personaId,
        byte level,
        uint totalExperience,
        ushort firstSkillId) =>
        new(
            exists: true,
            unknown0: 0,
            personaId,
            level,
            reservedAfterLevel: [0, 0, 0],
            totalExperience,
            skillIds: Enumerable.Range(firstSkillId, PersonaSlot.SkillCount).Select(static skillId => (ushort)skillId).ToArray(),
            strength: 11,
            magic: 22,
            endurance: 33,
            agility: 44,
            luck: 55);

    private sealed class FakeWorkingSave(WorkingSaveState state) : WorkingSave(state);

    private sealed class FakeSaveApplicationService : ISaveApplicationService
    {
        public Func<ReadOnlyMemory<byte>, SaveOpenResult<WorkingSave>>? OpenHandler { get; init; }

        public SaveOpenResult<WorkingSave> Open(ReadOnlyMemory<byte> bytes) =>
            OpenHandler?.Invoke(bytes) ?? throw new InvalidOperationException("OpenHandler must be configured.");

        public SaveEditResult<WorkingSave> ApplyEdits(WorkingSave save, IEnumerable<SaveEditCommand> edits) =>
            throw new NotSupportedException();

        public SaveWriteResult Write(WorkingSave save) =>
            throw new NotSupportedException();
    }
}
