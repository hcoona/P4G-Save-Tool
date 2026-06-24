using System.Collections.ObjectModel;

namespace P4G.SaveTool.Contracts;

public sealed class SaveEditResult<TSave>
{
    private readonly ReadOnlyCollection<SaveDiagnostic> diagnostics;

    public SaveEditResult(TSave? save, IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        Save = save;
        this.diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Succeeded = Save is not null &&
            !this.diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    public TSave? Save { get; }

    public IReadOnlyList<SaveDiagnostic> Diagnostics => diagnostics;

    public bool Succeeded { get; }
}
