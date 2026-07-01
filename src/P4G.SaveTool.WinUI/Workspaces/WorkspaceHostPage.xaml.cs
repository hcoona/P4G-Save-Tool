using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace P4G.SaveTool.WinUI;

public sealed partial class WorkspaceHostPage : Page
{
    public WorkspaceHostPage()
    {
        InitializeComponent();
    }

    internal void SetWorkspaceContent(UIElement content) =>
        WorkspaceContentHost.Content = content;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is UIElement content)
        {
            SetWorkspaceContent(content);
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        WorkspaceContentHost.Content = null;
        base.OnNavigatedFrom(e);
    }
}
