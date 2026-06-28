using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class CompendiumPersonaViewStateTests
{
    [Theory]
    [InlineData(0, "Orpheus", "#1 Orpheus")]
    [InlineData(3, "Jack Frost", "#4 Jack Frost")]
    public void DisplayNameFormatsSlotNumberUsingOneBasedIndex(int slotIndex, string name, string expectedDisplayName)
    {
        CompendiumPersonaViewState viewState = new(slotIndex, 0x0101, name, 1, 0);

        Assert.Equal(expectedDisplayName, viewState.DisplayName);
    }

    [Fact]
    public void ToStringIncludesNativeAotTemplateDisplayText()
    {
        CompendiumPersonaViewState viewState = new(0, 0x0101, "Izanagi", 12, 1234);

        Assert.Equal("#1 Izanagi  Lv 12  XP 1234", viewState.ToString());
    }
}
