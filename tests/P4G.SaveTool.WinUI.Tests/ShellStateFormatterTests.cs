using P4G.SaveTool.Contracts;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class ShellStateFormatterTests
{
    [Fact]
    public void GetWindowTitleUsesShellTitleWhenNoFileIsOpen()
    {
        Assert.Equal("P4G Save Tool", ShellStateFormatter.GetWindowTitle(null));
        Assert.Equal("P4G Save Tool", ShellStateFormatter.GetWindowTitle("   "));
    }

    [Fact]
    public void GetWindowTitleUsesCurrentFileNameWhenAFileIsOpen()
    {
        Assert.Equal(@"P4G Save Tool - data0001.bin", ShellStateFormatter.GetWindowTitle(@"Q:\saves\data0001.bin"));
    }

    [Fact]
    public void GetFilePathTextResetsToTheNoFileMessageWhenNoFileIsOpen()
    {
        Assert.Equal("No save file is open.", ShellStateFormatter.GetFilePathText(null));
        Assert.Equal("No save file is open.", ShellStateFormatter.GetFilePathText("   "));
    }

    [Fact]
    public void GetStatusTextFormatsBooleanFlags()
    {
        Assert.Equal("Has save: yes | Dirty: no | Can write: yes", ShellStateFormatter.GetStatusText(true, false, true));
    }

    [Fact]
    public void GetDiagnosticsTextReturnsResetTextWhenEmpty()
    {
        Assert.Equal(["No diagnostics."], ShellStateFormatter.GetDiagnosticsText(Array.Empty<SaveDiagnostic>()));
    }

    [Fact]
    public void GetDiagnosticsTextFormatsDiagnostics()
    {
        IReadOnlyList<string> diagnostics = ShellStateFormatter.GetDiagnosticsText(
            [
                new SaveDiagnostic(DiagnosticSeverity.Error, "CODE", "Boom", "Target"),
            ]);

        Assert.Equal(["Error CODE [Target]: Boom"], diagnostics);
    }

    [Fact]
    public void GetDiagnosticsTextFormatsTargetlessAndTargetedDiagnosticsInOrder()
    {
        IReadOnlyList<string> diagnostics = ShellStateFormatter.GetDiagnosticsText(
            [
                new SaveDiagnostic(DiagnosticSeverity.Warning, "WARN", "First", null),
                new SaveDiagnostic(DiagnosticSeverity.Error, "ERR", "Second", "Patch"),
            ]);

        Assert.Equal(
            [
                "Warning WARN: First",
                "Error ERR [Patch]: Second",
            ],
            diagnostics);
    }
}
