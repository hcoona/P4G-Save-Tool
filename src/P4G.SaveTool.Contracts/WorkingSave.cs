namespace P4G.SaveTool.Contracts;

public abstract class WorkingSave
{
    protected WorkingSave(WorkingSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        State = state;
    }

    public WorkingSaveState State { get; }
}
