using P4G.SaveTool.Contracts;
using P4G.SaveTool.SaveFormat;

namespace P4G.SaveTool.Application;

internal sealed class P4GApplicationSaveCodec : IApplicationSaveCodec
{
    public static P4GApplicationSaveCodec Instance { get; } = new();

    private P4GApplicationSaveCodec()
    {
    }

    public SaveOpenResult<SaveSnapshot> Open(ReadOnlyMemory<byte> bytes) =>
        P4GSaveCodec.Open(bytes);

    public SaveWriteResult Write(SaveSnapshot snapshot, IEnumerable<SaveFieldPatch> patches) =>
        P4GSaveCodec.Write(snapshot, patches);
}
