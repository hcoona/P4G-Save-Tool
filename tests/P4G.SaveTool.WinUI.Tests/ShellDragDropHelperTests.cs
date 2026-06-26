using Windows.ApplicationModel.DataTransfer;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class ShellDragDropHelperTests
{
    [Fact]
    public void GetAcceptedDragOperationReturnsCopyOnlyForOpenablePaths()
    {
        Assert.Equal(DataPackageOperation.Copy, ShellDragDropHelper.GetAcceptedDragOperation([@"Q:\saves\data0001.bin"]));
        Assert.Equal(DataPackageOperation.None, ShellDragDropHelper.GetAcceptedDragOperation(Array.Empty<string?>()));
        Assert.Equal(DataPackageOperation.None, ShellDragDropHelper.GetAcceptedDragOperation([""]));
    }

    [Fact]
    public void TryGetOpenablePathRejectsEmptySequences()
    {
        bool result = ShellDragDropHelper.TryGetOpenablePath(Array.Empty<string?>(), out string openablePath);

        Assert.False(result);
        Assert.Equal(string.Empty, openablePath);
    }

    [Fact]
    public void TryGetOpenablePathUsesTheFirstPathFromASequence()
    {
        bool result = ShellDragDropHelper.TryGetOpenablePath(
            [@"Q:\saves\first.bin", @"Q:\saves\second.bin"],
            out string openablePath);

        Assert.True(result);
        Assert.Equal(@"Q:\saves\first.bin", openablePath);
    }

    [Fact]
    public void TryGetOpenablePathRejectsEmptyFirstPathEvenWhenLaterPathsAreValid()
    {
        bool result = ShellDragDropHelper.TryGetOpenablePath(
            ["", @"Q:\saves\second.bin"],
            out string openablePath);

        Assert.False(result);
        Assert.Equal(string.Empty, openablePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetOpenablePathRejectsEmptyOrNonLocalPaths(string? path)
    {
        bool result = ShellDragDropHelper.TryGetOpenablePath(path, out string openablePath);

        Assert.False(result);
        Assert.Equal(string.Empty, openablePath);
    }

    [Fact]
    public void TryGetOpenablePathAcceptsLocalPaths()
    {
        bool result = ShellDragDropHelper.TryGetOpenablePath(@"Q:\saves\data0001.bin", out string openablePath);

        Assert.True(result);
        Assert.Equal(@"Q:\saves\data0001.bin", openablePath);
    }
}
