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

    internal static string GetStatusText(bool hasSave, bool hasPendingEditorDrafts, bool isDirty, bool canWrite)
    {
        if (!hasSave)
        {
            return "No save file open - open a save before editing.";
        }

        if (hasPendingEditorDrafts)
        {
            return "Dirty draft - apply edits before saving.";
        }

        if (isDirty && canWrite)
        {
            return "Applied pending write - save enabled.";
        }

        if (isDirty)
        {
            return "Write pending - waiting for save acknowledgement.";
        }

        return "Loaded clean - no unapplied changes.";
    }

    internal static IReadOnlyList<string> GetDiagnosticsText(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? ["No diagnostics."]
            : diagnostics.Select(FormatDiagnostic).ToArray();

    private static string FormatDiagnostic(SaveDiagnostic diagnostic) =>
        string.IsNullOrWhiteSpace(diagnostic.Target)
            ? string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}")
            : string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code} [{diagnostic.Target}]: {diagnostic.Message}");

}
