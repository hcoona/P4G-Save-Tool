using P4G.SaveTool.Contracts;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class SocialLinkBatchTests
{
    [Fact]
    public void SocialLinkEditBatchBuilderAssemblesAllEditsWhenInputsAreValid()
    {
        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(2, "6", "4", "2", edits, diagnostics);

        Assert.True(succeeded);
        Assert.Equal(
            [
                new SetSocialLinkLevelEdit(2, 6),
                new SetSocialLinkProgressEdit(2, 4),
                new SetSocialLinkFlagEdit(2, 2),
            ],
            edits);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SocialLinkEditBatchBuilderSkipsValidationWhenNoSocialLinkIsSelected()
    {
        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(null, "not-a-number", "-1", "256", edits, diagnostics);

        Assert.True(succeeded);
        Assert.Empty(edits);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("", "4", "2", "Level", "SocialLinks.Level")]
    [InlineData("6", "-1", "2", "Progress", "SocialLinks.Progress")]
    [InlineData("6", "4", "256", "Flag", "SocialLinks.Flag")]
    public void SocialLinkEditBatchBuilderReportsFieldSpecificDiagnosticsAndBlocksEdits(
        string levelText,
        string progressText,
        string flagText,
        string fieldName,
        string expectedTarget)
    {
        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(2, levelText, progressText, flagText, edits, diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Single(diagnostics);
        Assert.Equal("P4GWINUI024", diagnostics[0].Code);
        Assert.Equal(expectedTarget, diagnostics[0].Target);
        Assert.Equal($"{fieldName} must be a whole number from 0 to 255.", diagnostics[0].Message);
    }

    [Fact]
    public void SocialLinkEditBatchBuilderAccumulatesAllInvalidFieldDiagnostics()
    {
        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(2, "-1", "abc", "256", edits, diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Equal(3, diagnostics.Count);
        Assert.Collection(
            diagnostics,
            diagnostic =>
            {
                Assert.Equal("P4GWINUI024", diagnostic.Code);
                Assert.Equal("SocialLinks.Level", diagnostic.Target);
                Assert.Equal("Level must be a whole number from 0 to 255.", diagnostic.Message);
            },
            diagnostic =>
            {
                Assert.Equal("P4GWINUI024", diagnostic.Code);
                Assert.Equal("SocialLinks.Progress", diagnostic.Target);
                Assert.Equal("Progress must be a whole number from 0 to 255.", diagnostic.Message);
            },
            diagnostic =>
            {
                Assert.Equal("P4GWINUI024", diagnostic.Code);
                Assert.Equal("SocialLinks.Flag", diagnostic.Target);
                Assert.Equal("Flag must be a whole number from 0 to 255.", diagnostic.Message);
            });
    }
}
