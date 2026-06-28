namespace P4G.SaveTool.Presentation;

public sealed record PartyConfigurationChoiceViewState(byte MemberValue, string Name, bool IsUnknown = false)
{
    public override string ToString() => Name;
}
