using P4G.SaveTool.Application;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class MainWindowStateTests
{
    [Fact]
    public void ConsumeStartupOpenPathReturnsPathOnceAndClearsBacker()
    {
        string? startupOpenPath = @"Q:\saves\data0001.bin";

        string? consumedPath = MainWindow.ConsumeStartupOpenPath(ref startupOpenPath);

        Assert.Equal(@"Q:\saves\data0001.bin", consumedPath);
        Assert.Null(startupOpenPath);
        Assert.Null(MainWindow.ConsumeStartupOpenPath(ref startupOpenPath));
    }

    [Fact]
    public void TryBeginBusyOperationBlocksReentryUntilReset()
    {
        bool isBusy = false;

        Assert.True(MainWindow.TryBeginBusyOperation(ref isBusy));
        Assert.True(isBusy);
        Assert.False(MainWindow.TryBeginBusyOperation(ref isBusy));

        MainWindow.EndBusyOperation(ref isBusy);

        Assert.False(isBusy);
        Assert.True(MainWindow.TryBeginBusyOperation(ref isBusy));
    }

    [Fact]
    public void RestoreNoSaveStateAfterFailedBlankSaveCoreClearsBlankStateAndKeepsDiagnosticsOverride()
    {
        SaveEditorViewModel viewModel = new(new SaveApplicationService());
        SaveEditorOperationResult createBlankResult = viewModel.CreateBlankSave();
        Assert.True(createBlankResult.Succeeded);
        Assert.True(viewModel.HasSave);

        IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride = null;
        bool refreshCalled = false;
        IReadOnlyList<SaveDiagnostic> diagnostics =
        [
            new SaveDiagnostic(DiagnosticSeverity.Error, "P4GWINUI004", "Could not write the save file: boom", "Persistence"),
        ];

        MainWindow.RestoreNoSaveStateAfterFailedBlankSaveCore(
            new SaveEditorRefreshCoordinator(),
            viewModel,
            diagnostics,
            () => refreshCalled = true,
            ref uiDiagnosticsOverride);

        Assert.True(refreshCalled);
        Assert.Same(diagnostics, uiDiagnosticsOverride);
        Assert.False(viewModel.HasSave);
        Assert.False(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.FamilyName);
        Assert.Equal(string.Empty, viewModel.GivenName);
        Assert.Equal(0u, viewModel.Yen);
        Assert.Empty(viewModel.Diagnostics);
    }
}
