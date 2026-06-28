namespace P4G.SaveTool.Presentation;

public sealed record SkillChoiceViewState(ushort SkillId, string Name, bool IsUnknown = false)
{
    public override string ToString() => Name;
}
