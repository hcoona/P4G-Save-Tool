namespace P4G.SaveTool.Contracts;

public sealed record SaveDiagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Target = null);
