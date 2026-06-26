using System.Globalization;
using System.IO;
using System.Linq;
using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.WinUI;

internal static class ShellStateFormatter
{
    internal const string ShellTitle = "P4G Save Tool";

    internal static string GetWindowTitle(string? currentFilePath) =>
        string.IsNullOrWhiteSpace(currentFilePath)
            ? ShellTitle
            : $"{ShellTitle} - {Path.GetFileName(currentFilePath)}";

    internal static string GetFilePathText(string? currentFilePath) =>
        string.IsNullOrWhiteSpace(currentFilePath)
            ? "No save file is open."
            : currentFilePath;

    internal static string GetStatusText(bool hasSave, bool isDirty, bool canWrite) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Has save: {FormatBoolean(hasSave)} | Dirty: {FormatBoolean(isDirty)} | Can write: {FormatBoolean(canWrite)}");

    internal static IReadOnlyList<string> GetDiagnosticsText(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? ["No diagnostics."]
            : diagnostics.Select(FormatDiagnostic).ToArray();

    private static string FormatDiagnostic(SaveDiagnostic diagnostic) =>
        string.IsNullOrWhiteSpace(diagnostic.Target)
            ? string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}")
            : string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code} [{diagnostic.Target}]: {diagnostic.Message}");

    private static string FormatBoolean(bool value) =>
        value ? "yes" : "no";
}
