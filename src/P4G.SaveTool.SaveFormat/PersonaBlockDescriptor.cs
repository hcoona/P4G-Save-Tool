namespace P4G.SaveTool.SaveFormat;

public sealed record PersonaBlockDescriptor(
    string Name,
    int Offset,
    int Count,
    int Stride,
    int PersonaOffsetWithinStride = 0,
    int? BlockPatchLength = null)
{
    public int EndOffset => Offset + (Count * Stride);

    public int EffectiveBlockPatchLength => BlockPatchLength ?? Count * Stride;
}
