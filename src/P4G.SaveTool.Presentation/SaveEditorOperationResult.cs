using System.Collections.ObjectModel;
using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.Presentation;

public sealed class SaveEditorOperationResult
{
    private readonly ReadOnlyCollection<SaveDiagnostic> diagnostics;

    public SaveEditorOperationResult(bool succeeded, IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        this.diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Succeeded = succeeded &&
            !this.diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    public bool Succeeded { get; }

    public IReadOnlyList<SaveDiagnostic> Diagnostics => diagnostics;
}
