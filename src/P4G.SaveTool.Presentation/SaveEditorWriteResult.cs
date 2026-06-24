using System.Collections.ObjectModel;
using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.Presentation;

public sealed class SaveEditorWriteResult
{
    private readonly byte[]? bytes;
    private readonly ReadOnlyCollection<SaveDiagnostic> diagnostics;

    private SaveEditorWriteResult(
        byte[]? bytes,
        SaveEditorWriteToken? operationToken,
        IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        this.bytes = bytes?.ToArray();
        this.diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        OperationToken = operationToken;
        Succeeded = this.bytes is not null &&
            OperationToken.HasValue &&
            !this.diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    public byte[]? Bytes => bytes?.ToArray();

    public SaveEditorWriteToken? OperationToken { get; }

    public IReadOnlyList<SaveDiagnostic> Diagnostics => diagnostics;

    public bool Succeeded { get; }

    internal static SaveEditorWriteResult FromApplicationResult(
        SaveWriteResult result,
        SaveEditorWriteToken? operationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SaveEditorWriteResult(result.Bytes, operationToken, result.Diagnostics);
    }

    internal static SaveEditorWriteResult Failure(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        new(null, null, diagnostics);
}
