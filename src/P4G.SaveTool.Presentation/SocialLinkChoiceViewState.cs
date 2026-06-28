namespace P4G.SaveTool.Presentation;

public sealed record SocialLinkChoiceViewState(
    byte LinkId,
    string Name,
    string ArcanaName,
    bool IsPlaceholder = false,
    bool IsUnknown = false)
{
    public override string ToString() => Name;
}
