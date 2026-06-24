namespace P4G.SaveTool.Presentation;

public readonly struct SaveEditorWriteToken : IEquatable<SaveEditorWriteToken>
{
    private readonly long value;

    internal SaveEditorWriteToken(long value)
    {
        this.value = value;
    }

    public bool Equals(SaveEditorWriteToken other) =>
        value == other.value;

    public override bool Equals(object? obj) =>
        obj is SaveEditorWriteToken other && Equals(other);

    public override int GetHashCode() =>
        value.GetHashCode();

    public static bool operator ==(SaveEditorWriteToken left, SaveEditorWriteToken right) =>
        left.Equals(right);

    public static bool operator !=(SaveEditorWriteToken left, SaveEditorWriteToken right) =>
        !left.Equals(right);
}
