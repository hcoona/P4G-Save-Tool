using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class LaunchArgumentParserTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(@"Q:\saves\data0001.bin", @"Q:\saves\data0001.bin")]
    [InlineData(@"""Q:\saves with spaces\data 0001.bin""", @"Q:\saves with spaces\data 0001.bin")]
    [InlineData(@"  ""Q:\saves with spaces\data 0001.bin""	--flag  ", @"Q:\saves with spaces\data 0001.bin")]
    public void GetOpenPathParsesLaunchArguments(string? arguments, string? expectedPath)
    {
        string? actualPath = LaunchArgumentParser.GetOpenPath(arguments);

        Assert.Equal(expectedPath, actualPath);
    }

    [Fact]
    public void GetOpenPathFallsBackToCommandLineArguments()
    {
        string? actualPath = LaunchArgumentParser.GetOpenPath(
            null,
            [@"P4G.SaveTool.WinUI.exe", @"Q:\saves\direct-launch.bin"]);

        Assert.Equal(@"Q:\saves\direct-launch.bin", actualPath);
    }

    [Fact]
    public void GetOpenPathPrefersLaunchActivatedArgumentsOverCommandLineArguments()
    {
        string? actualPath = LaunchArgumentParser.GetOpenPath(
            @"""Q:\saves\activated-launch.bin""",
            [@"P4G.SaveTool.WinUI.exe", @"Q:\saves\direct-launch.bin"]);

        Assert.Equal(@"Q:\saves\activated-launch.bin", actualPath);
    }
}
