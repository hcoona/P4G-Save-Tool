namespace P4G.SaveTool.Presentation;

public sealed record SocialLinkViewState(
    int SlotIndex,
    byte LinkId,
    string Name,
    string ArcanaName,
    byte Level,
    byte Progress,
    byte Flag,
    bool IsUnknown = false)
{
    public string DisplayName => string.IsNullOrEmpty(ArcanaName)
        ? Name
        : $"{Name} [{ArcanaName}]";
}
