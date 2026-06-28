namespace P4G.SaveTool.Presentation;

public sealed record SocialStatRankChoiceViewState(int Rank, string Name)
{
    public override string ToString() => Name;
}
