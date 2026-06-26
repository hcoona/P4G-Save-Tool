namespace P4G.SaveTool.Contracts;

public interface ISaveApplicationService
{
    SaveOpenResult<WorkingSave> Open(ReadOnlyMemory<byte> bytes);

    SaveOpenResult<WorkingSave> CreateBlankSave();

    SaveEditResult<WorkingSave> ApplyEdits(WorkingSave save, IEnumerable<SaveEditCommand> edits);

    SaveWriteResult Write(WorkingSave save);
}
