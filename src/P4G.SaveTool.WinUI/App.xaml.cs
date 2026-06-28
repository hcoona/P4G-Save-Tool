namespace P4G.SaveTool.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Microsoft.UI.Xaml.Window? window;

    public App()
    {
        XamlBindingPreservation.PreserveXamlBindingProperties();
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        string? openPath = LaunchArgumentParser.GetOpenPath(args.Arguments, Environment.GetCommandLineArgs());
        window = new MainWindow(openPath);
        window.Activate();
    }
}
