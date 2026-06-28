namespace P4G.SaveTool.Presentation;

public sealed record PersonaChoiceViewState(ushort PersonaId, string Name, bool IsUnknown = false)
{
    public override string ToString() => Name;
}
