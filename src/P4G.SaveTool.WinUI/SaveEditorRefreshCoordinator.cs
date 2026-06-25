namespace P4G.SaveTool.WinUI;

internal sealed class SaveEditorRefreshCoordinator
{
    private int suppressionDepth;

    internal bool IsFullRefreshSuppressed => suppressionDepth > 0;

    internal TResult RunWithFullRefreshSuppressed<TResult>(Func<TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        suppressionDepth++;
        try
        {
            return operation();
        }
        finally
        {
            suppressionDepth--;
        }
    }
}
