using P4G.SaveTool.Contracts;
using P4G.SaveTool.SaveFormat;

namespace P4G.SaveTool.Application;

internal sealed class ApplicationWorkingSave : WorkingSave
{
    internal ApplicationWorkingSave(SaveSnapshot snapshot, WorkingSaveState state)
        : base(state)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Snapshot = snapshot;
    }

    internal SaveSnapshot Snapshot { get; }

    internal ApplicationWorkingSave WithState(WorkingSaveState state) => new(Snapshot, state);
}
