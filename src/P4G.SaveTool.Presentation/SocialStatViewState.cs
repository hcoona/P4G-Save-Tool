namespace P4G.SaveTool.Presentation;

public sealed record SocialStatViewState(
    int StatIndex,
    string Name,
    ushort Points,
    int Rank,
    string RankName);
