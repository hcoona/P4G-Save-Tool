namespace P4G.SaveTool.SaveFormat;

public sealed record SaveFieldDescriptor(string Name, int Offset, int Length)
{
    public int EndOffset => Offset + Length;
}
