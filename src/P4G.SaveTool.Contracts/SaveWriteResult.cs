using System.Collections.ObjectModel;

namespace P4G.SaveTool.Contracts;

public sealed class SaveWriteResult
{
    private readonly byte[]? bytes;
    private readonly ReadOnlyCollection<SaveDiagnostic> diagnostics;

    public SaveWriteResult(byte[]? bytes, IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        this.bytes = bytes?.ToArray();
        this.diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Succeeded = this.bytes is not null &&
            !this.diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    public byte[]? Bytes => bytes?.ToArray();

    public IReadOnlyList<SaveDiagnostic> Diagnostics => diagnostics;

    public bool Succeeded { get; }

    public static SaveWriteResult Success(byte[] bytes, IReadOnlyList<SaveDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return new(bytes, diagnostics ?? []);
    }

    public static SaveWriteResult Failure(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        new(null, diagnostics);
}
