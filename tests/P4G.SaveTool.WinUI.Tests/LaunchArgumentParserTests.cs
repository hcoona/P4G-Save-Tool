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
}
