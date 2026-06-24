using P4G.SaveTool.Contracts;
using P4G.SaveTool.SaveFormat;

namespace P4G.SaveTool.Application;

internal interface IApplicationSaveCodec
{
    SaveOpenResult<SaveSnapshot> Open(ReadOnlyMemory<byte> bytes);

    SaveWriteResult Write(SaveSnapshot snapshot, IEnumerable<SaveFieldPatch> patches);
}
