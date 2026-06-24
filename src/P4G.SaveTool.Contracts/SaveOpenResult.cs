using System.Collections.ObjectModel;

namespace P4G.SaveTool.Contracts;

public sealed class SaveOpenResult<TSnapshot>
{
    private readonly ReadOnlyCollection<SaveDiagnostic> diagnostics;

    public SaveOpenResult(TSnapshot? snapshot, IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        Snapshot = snapshot;
        this.diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Succeeded = Snapshot is not null &&
            !this.diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    public TSnapshot? Snapshot { get; }

    public IReadOnlyList<SaveDiagnostic> Diagnostics => diagnostics;

    public bool Succeeded { get; }
}
