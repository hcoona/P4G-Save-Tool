using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class SocialLinkDraftTests
{
    [Fact]
    public void SocialLinkDraftIsRestoredOnlyWhenSelectedLinkMatches()
    {
        MainWindow.SocialLinkDraftState draft = new(3, 12, "6", "4", "2");
        SocialLinkViewState matchingLink = new(3, 12, "Matching", string.Empty, 1, 1, 1);
        SocialLinkViewState shiftedLink = new(2, 12, "Matching", string.Empty, 1, 1, 1);
        SocialLinkViewState changedLink = new(3, 13, "Matching", string.Empty, 1, 1, 1);

        Assert.True(MainWindow.ShouldRestoreSelectedSocialLinkDraft(draft, matchingLink));
        Assert.True(MainWindow.ShouldRestoreSelectedSocialLinkDraft(draft, shiftedLink));
        Assert.False(MainWindow.ShouldRestoreSelectedSocialLinkDraft(draft, changedLink));
        Assert.False(MainWindow.ShouldRestoreSelectedSocialLinkDraft(draft, null));
    }

    [Fact]
    public void SocialLinkDraftRefreshHelperRestoresAfterSelectedLinkShifts()
    {
        MainWindow.SocialLinkDraftState draft = new(3, 12, "6", "4", "2");
        SocialLinkViewState matchingLink = new(3, 12, "Matching", string.Empty, 1, 1, 1);
        SocialLinkViewState shiftedLink = new(2, 12, "Matching", string.Empty, 1, 1, 1);

        static SaveEditorOperationResult CreateSuccessResult() =>
            new(true, Array.Empty<SaveDiagnostic>());

        bool captureRan = false;
        bool refreshRan = false;
        int restoreCalls = 0;
        MainWindow.SocialLinkDraftState? restoredDraft = null;
        SocialLinkViewState? selectedLink = matchingLink;

        SaveEditorOperationResult result = MainWindow.RefreshSocialLinkDraftPreservingSelection(
            () =>
            {
                Assert.False(refreshRan);
                captureRan = true;
                return draft;
            },
            () =>
            {
                Assert.True(captureRan);
                return CreateSuccessResult();
            },
            () =>
            {
                Assert.True(captureRan);
                refreshRan = true;
                selectedLink = shiftedLink;
            },
            () => selectedLink,
            socialLinkDraft =>
            {
                restoreCalls++;
                restoredDraft = socialLinkDraft;
            });

        Assert.True(result.Succeeded);
        Assert.True(captureRan);
        Assert.True(refreshRan);
        Assert.Equal(1, restoreCalls);
        Assert.Equal(draft, restoredDraft);
    }

    [Fact]
    public void SocialLinkSelectionResolverPrefersLinkIdAfterEarlierSlotDeletes()
    {
        IReadOnlyList<SocialLinkViewState> socialLinks =
        [
            new(0, 1, "First", string.Empty, 1, 0, 0),
            new(1, 3, "Selected", string.Empty, 2, 1, 0),
        ];

        SocialLinkViewState? selectedLink = MainWindow.ResolveSelectedSocialLinkViewState(socialLinks, 2, 3);

        Assert.Same(socialLinks[1], selectedLink);
    }

    [Fact]
    public void SocialLinkSelectionResolverFallsBackToSlotIndexWhenLinkIdIsStale()
    {
        IReadOnlyList<SocialLinkViewState> socialLinks =
        [
            new(0, 1, "First", string.Empty, 1, 0, 0),
            new(1, 3, "Selected", string.Empty, 2, 1, 0),
        ];

        SocialLinkViewState? selectedLink = MainWindow.ResolveSelectedSocialLinkViewState(socialLinks, 1, 99);

        Assert.Same(socialLinks[1], selectedLink);
    }

    [Fact]
    public void SocialLinkSelectionResolverFallsBackToFirstLinkWhenSelectionIsStale()
    {
        IReadOnlyList<SocialLinkViewState> socialLinks =
        [
            new(0, 1, "First", string.Empty, 1, 0, 0),
            new(1, 3, "Second", string.Empty, 2, 1, 0),
        ];

        SocialLinkViewState? selectedLink = MainWindow.ResolveSelectedSocialLinkViewState(socialLinks, 9, 99);

        Assert.Same(socialLinks[0], selectedLink);
    }

    [Fact]
    public void SocialLinkSelectionResolverReturnsNullWhenNoEntriesExist()
    {
        SocialLinkViewState? selectedLink = MainWindow.ResolveSelectedSocialLinkViewState(
            Array.Empty<SocialLinkViewState>(),
            selectedSocialLinkIndex: null,
            selectedSocialLinkLinkId: null);

        Assert.Null(selectedLink);
    }

    [Fact]
    public void SocialLinkSelectionResetHelperClearsBothSelectionFields()
    {
        int? selectedSocialLinkIndex = 3;
        byte? selectedSocialLinkLinkId = 12;

        MainWindow.ResetSelectedSocialLinkState(ref selectedSocialLinkIndex, ref selectedSocialLinkLinkId);

        Assert.Null(selectedSocialLinkIndex);
        Assert.Null(selectedSocialLinkLinkId);
    }

    [Fact]
    public void SocialLinkDraftRefreshHelperSkipsRestoreWhenSelectionClearsAfterDeletingLastLink()
    {
        MainWindow.SocialLinkDraftState draft = new(0, 12, "6", "4", "2");

        static SaveEditorOperationResult CreateSuccessResult() =>
            new(true, Array.Empty<SaveDiagnostic>());

        bool captureRan = false;
        bool refreshRan = false;
        int restoreCalls = 0;
        MainWindow.SocialLinkDraftState? restoredDraft = null;
        SocialLinkViewState? selectedLink = new(0, 12, "Selected", string.Empty, 1, 1, 1);

        SaveEditorOperationResult result = MainWindow.RefreshSocialLinkDraftPreservingSelection(
            () =>
            {
                Assert.False(refreshRan);
                captureRan = true;
                return draft;
            },
            () =>
            {
                Assert.True(captureRan);
                return CreateSuccessResult();
            },
            () =>
            {
                Assert.True(captureRan);
                refreshRan = true;
                selectedLink = null;
            },
            () => selectedLink,
            socialLinkDraft =>
            {
                restoreCalls++;
                restoredDraft = socialLinkDraft;
            });

        Assert.True(result.Succeeded);
        Assert.True(captureRan);
        Assert.True(refreshRan);
        Assert.Equal(0, restoreCalls);
        Assert.Null(restoredDraft);
        Assert.Null(selectedLink);
    }

    [Fact]
    public void SocialLinkDraftRefreshHelperCapturesRefreshesAndSkipsDifferentLink()
    {
        MainWindow.SocialLinkDraftState draft = new(3, 12, "6", "4", "2");
        SocialLinkViewState matchingLink = new(3, 12, "Matching", string.Empty, 1, 1, 1);
        SocialLinkViewState changedLink = new(4, 13, "Matching", string.Empty, 1, 1, 1);

        static SaveEditorOperationResult CreateSuccessResult() =>
            new(true, Array.Empty<SaveDiagnostic>());

        bool captureRan = false;
        bool refreshRan = false;
        int restoreCalls = 0;
        MainWindow.SocialLinkDraftState? restoredDraft = null;
        SocialLinkViewState? selectedLink = matchingLink;

        SaveEditorOperationResult result = MainWindow.RefreshSocialLinkDraftPreservingSelection(
            () =>
            {
                Assert.False(refreshRan);
                captureRan = true;
                return draft;
            },
            () =>
            {
                Assert.True(captureRan);
                return CreateSuccessResult();
            },
            () =>
            {
                Assert.True(captureRan);
                refreshRan = true;
            },
            () => selectedLink,
            socialLinkDraft =>
            {
                restoreCalls++;
                restoredDraft = socialLinkDraft;
            });

        Assert.True(result.Succeeded);
        Assert.True(captureRan);
        Assert.True(refreshRan);
        Assert.Equal(1, restoreCalls);
        Assert.Equal(draft, restoredDraft);

        captureRan = false;
        refreshRan = false;
        restoreCalls = 0;
        restoredDraft = null;
        selectedLink = matchingLink;

        result = MainWindow.RefreshSocialLinkDraftPreservingSelection(
            () =>
            {
                Assert.False(refreshRan);
                captureRan = true;
                return draft;
            },
            () =>
            {
                Assert.True(captureRan);
                return CreateSuccessResult();
            },
            () =>
            {
                Assert.True(captureRan);
                refreshRan = true;
                selectedLink = changedLink;
            },
            () => selectedLink,
            socialLinkDraft =>
            {
                restoreCalls++;
                restoredDraft = socialLinkDraft;
            });

        Assert.True(result.Succeeded);
        Assert.True(captureRan);
        Assert.True(refreshRan);
        Assert.Equal(0, restoreCalls);
        Assert.Null(restoredDraft);
    }

    [Fact]
    public void SocialLinkDraftAfterApplyIsPreservedOnlyWhenBatchDoesNotTouchSocialLinks()
    {
        Assert.True(MainWindow.ShouldPreserveSelectedSocialLinkDraftAfterApply(Array.Empty<SaveEditCommand>()));

        Assert.True(MainWindow.ShouldPreserveSelectedSocialLinkDraftAfterApply(
            [new SetYenEdit(123), new SetSaveNamesEdit("Family", "Given")]));

        Assert.False(MainWindow.ShouldPreserveSelectedSocialLinkDraftAfterApply(
            [new SetSocialLinkLevelEdit(2, 6)]));

        Assert.False(MainWindow.ShouldPreserveSelectedSocialLinkDraftAfterApply(
            [new SetSocialLinkProgressEdit(2, 4)]));

        Assert.False(MainWindow.ShouldPreserveSelectedSocialLinkDraftAfterApply(
            [new SetSocialLinkFlagEdit(2, 1)]));
    }
}
