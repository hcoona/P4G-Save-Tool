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

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(2, "6", "4", edits, diagnostics);

        Assert.True(succeeded);
        Assert.Equal(
            [
                new SetSocialLinkLevelEdit(2, 6),
                new SetSocialLinkProgressEdit(2, 4),
            ],
            edits);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SocialLinkEditBatchBuilderSkipsValidationWhenNoSocialLinkIsSelected()
    {
        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(null, "not-a-number", "-1", edits, diagnostics);

        Assert.True(succeeded);
        Assert.Empty(edits);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("", "4", "Level", "SocialLinks.Level")]
    [InlineData("6", "-1", "Progress", "SocialLinks.Progress")]
    public void SocialLinkEditBatchBuilderReportsFieldSpecificDiagnosticsAndBlocksEdits(
        string levelText,
        string progressText,
        string fieldName,
        string expectedTarget)
    {
        List<SaveEditCommand> edits = [];
        List<SaveDiagnostic> diagnostics = [];

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(2, levelText, progressText, edits, diagnostics);

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

        bool succeeded = MainWindow.TryBuildSocialLinkEdits(2, "-1", "abc", edits, diagnostics);

        Assert.False(succeeded);
        Assert.Empty(edits);
        Assert.Equal(2, diagnostics.Count);
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
            });
    }
}
