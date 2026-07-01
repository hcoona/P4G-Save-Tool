using System.Collections;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace P4G.SaveTool.WinUI;

public sealed partial class DiagnosticsWorkspacePage : Page
{
    private INotifyCollectionChanged? diagnosticsCollection;

    public DiagnosticsWorkspacePage()
    {
        InitializeComponent();
    }

    internal void SetDiagnosticsItems(object? itemsSource)
    {
        if (diagnosticsCollection is not null)
        {
            diagnosticsCollection.CollectionChanged -= DiagnosticsCollection_CollectionChanged;
        }

        DiagnosticsListView.ItemsSource = itemsSource;
        diagnosticsCollection = itemsSource as INotifyCollectionChanged;
        if (diagnosticsCollection is not null)
        {
            diagnosticsCollection.CollectionChanged += DiagnosticsCollection_CollectionChanged;
        }

        UpdateDiagnosticsVisualState();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        SetDiagnosticsItems(e.Parameter);
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (diagnosticsCollection is not null)
        {
            diagnosticsCollection.CollectionChanged -= DiagnosticsCollection_CollectionChanged;
            diagnosticsCollection = null;
        }

        base.OnNavigatedFrom(e);
    }

    private void DiagnosticsCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateDiagnosticsVisualState();

    private void UpdateDiagnosticsVisualState()
    {
        bool hasDiagnostics = HasDiagnostics(DiagnosticsListView.ItemsSource);
        DiagnosticsListView.Visibility = hasDiagnostics ? Visibility.Visible : Visibility.Collapsed;
        NoDiagnosticsTextBlock.Visibility = hasDiagnostics ? Visibility.Collapsed : Visibility.Visible;
    }

    private static bool HasDiagnostics(object? itemsSource)
    {
        if (itemsSource is not IEnumerable items)
        {
            return false;
        }

        foreach (object? item in items)
        {
            if (item is DiagnosticListItemViewState { Code: not "Status" })
            {
                return true;
            }
        }

        return false;
    }
}
