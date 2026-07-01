using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace P4G.SaveTool.WinUI;

internal readonly record struct OverviewWorkspaceViewState(
    bool HasSave,
    string FilePathText,
    string StateText);

public sealed partial class OverviewWorkspacePage : Page
{
    public event EventHandler<RoutedEventArgs>? OpenSaveRequested;

    public OverviewWorkspacePage()
    {
        InitializeComponent();
    }

    internal void SetOverviewState(OverviewWorkspaceViewState state)
    {
        OverviewNoSaveEmptyStateBorder.Visibility = state.HasSave ? Visibility.Collapsed : Visibility.Visible;
        OverviewLoadedContent.Visibility = state.HasSave ? Visibility.Visible : Visibility.Collapsed;
        OverviewFilePathTextBlock.Text = state.FilePathText;
        OverviewStateTextBlock.Text = state.StateText;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is OverviewWorkspaceViewState state)
        {
            SetOverviewState(state);
        }

        base.OnNavigatedTo(e);
    }

    private void OverviewOpenButton_Click(object sender, RoutedEventArgs e) =>
        OpenSaveRequested?.Invoke(this, e);
}
