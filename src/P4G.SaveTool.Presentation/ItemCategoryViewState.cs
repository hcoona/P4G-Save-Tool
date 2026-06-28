namespace P4G.SaveTool.Presentation;

public sealed record ItemCategoryViewState(byte CategoryId, string Name)
{
    public override string ToString() => Name;
}
