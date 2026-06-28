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
}
