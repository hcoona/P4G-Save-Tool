using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class OpenSaveDraftPreservationTests
{
    [Fact]
    public void FailedOpenPreservesInventoryAndComplexDraftState()
    {
        string inventoryQuantityDraft = "37";
        int? selectedInventoryEntryId = 1025;
        string socialLinkLevelDraft = "6";
        string compendiumExperienceDraft = "9000";
        bool refreshCalled = false;
        bool preserveCalled = false;

        MainWindow.ApplyOpenResult(
            succeeded: false,
            refreshEditorState: () =>
            {
                refreshCalled = true;
                inventoryQuantityDraft = "1";
                selectedInventoryEntryId = null;
                socialLinkLevelDraft = "1";
                compendiumExperienceDraft = "0";
            },
            preserveEditorState: () =>
            {
                preserveCalled = true;
            });

        Assert.False(refreshCalled);
        Assert.True(preserveCalled);
        Assert.Equal("37", inventoryQuantityDraft);
        Assert.Equal(1025, selectedInventoryEntryId);
        Assert.Equal("6", socialLinkLevelDraft);
        Assert.Equal("9000", compendiumExperienceDraft);
    }

    [Fact]
    public void SuccessfulOpenRefreshesInventoryAndComplexDraftState()
    {
        string inventoryQuantityDraft = "37";
        int? selectedInventoryEntryId = 1025;
        string socialLinkLevelDraft = "6";
        string compendiumExperienceDraft = "9000";
        bool refreshCalled = false;
        bool preserveCalled = false;

        MainWindow.ApplyOpenResult(
            succeeded: true,
            refreshEditorState: () =>
            {
                refreshCalled = true;
                inventoryQuantityDraft = string.Empty;
                selectedInventoryEntryId = null;
                socialLinkLevelDraft = string.Empty;
                compendiumExperienceDraft = string.Empty;
            },
            preserveEditorState: () =>
            {
                preserveCalled = true;
            });

        Assert.True(refreshCalled);
        Assert.False(preserveCalled);
        Assert.Empty(inventoryQuantityDraft);
        Assert.Null(selectedInventoryEntryId);
        Assert.Empty(socialLinkLevelDraft);
        Assert.Empty(compendiumExperienceDraft);
    }
}
