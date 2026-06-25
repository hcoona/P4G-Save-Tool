using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class SaveEditorRefreshCoordinatorTests
{
    [Fact]
    public void SuppressedSaveRefreshDefersFullRefreshDuringWriteAndAcknowledge()
    {
        SaveEditorRefreshCoordinator coordinator = new();
        int fullRefreshCount = 0;
        int preservedRefreshCount = 0;

        coordinator.RunWithFullRefreshSuppressed(() =>
        {
            if (coordinator.IsFullRefreshSuppressed)
            {
                preservedRefreshCount++;
            }
            else
            {
                fullRefreshCount++;
            }

            if (coordinator.IsFullRefreshSuppressed)
            {
                preservedRefreshCount++;
            }
            else
            {
                fullRefreshCount++;
            }

            return 0;
        });

        Assert.Equal(0, fullRefreshCount);
        Assert.Equal(2, preservedRefreshCount);
        Assert.False(coordinator.IsFullRefreshSuppressed);
    }
}
