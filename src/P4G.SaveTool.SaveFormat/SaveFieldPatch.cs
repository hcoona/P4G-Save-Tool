namespace P4G.SaveTool.SaveFormat;

public sealed class SaveFieldPatch
{
    private readonly byte[] bytes;

    public SaveFieldPatch(string fieldName, ReadOnlyMemory<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        FieldName = fieldName;
        this.bytes = bytes.ToArray();
    }

    public string FieldName { get; }

    public int ByteLength => bytes.Length;

    public ReadOnlyMemory<byte> Bytes => bytes.ToArray();

    internal ReadOnlySpan<byte> BytesSpan => bytes;
}
