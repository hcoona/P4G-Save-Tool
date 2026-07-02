using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

public sealed partial class CompendiumWorkspacePage : Page
{
    public CompendiumWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    internal event SelectionChangedEventHandler? CompendiumSelectionChanged;
    internal event TappedEventHandler? CompendiumTapped;
    internal event SelectionChangedEventHandler? CompendiumAddSelectionChanged;
    internal event RoutedEventHandler? CompendiumRemoveClick;
    internal event RoutedEventHandler? CompendiumClearClick;

    internal CompendiumPersonaViewState? SelectedCompendiumEntry
    {
        get => CompendiumListView.SelectedItem as CompendiumPersonaViewState;
        set => CompendiumListView.SelectedItem = value;
    }

    internal PersonaChoiceViewState? SelectedCompendiumAddChoice
    {
        get => CompendiumAddComboBox.SelectedItem as PersonaChoiceViewState;
        set => CompendiumAddComboBox.SelectedItem = value;
    }

    internal string PersonaSummaryText
    {
        get => PersonaSummaryTextBox.Text ?? string.Empty;
        set => PersonaSummaryTextBox.Text = value;
    }

    internal string SelectedCompendiumEntryDescription =>
        CompendiumListView.SelectedItem?.ToString() ?? "the selected compendium entry";

    internal void SetEditorContextText(string text) =>
        CompendiumEditorContextTextBlock.Text = text;

    internal void SetItemsSources(object compendiumEntriesSource, object compendiumAddChoicesSource)
    {
        CompendiumListView.ItemsSource = compendiumEntriesSource;
        CompendiumAddComboBox.ItemsSource = compendiumAddChoicesSource;
    }

    internal void SetCompendiumEnabled(bool isEnabled, bool hasSelectedEntry, bool hasEntries)
    {
        CompendiumListView.IsEnabled = isEnabled;
        CompendiumAddComboBox.IsEnabled = isEnabled;
        CompendiumRemoveButton.IsEnabled = isEnabled && hasSelectedEntry;
        CompendiumClearButton.IsEnabled = isEnabled && hasEntries;
    }

    internal void SetPersonaEditor(PersonaEditorControl editor) =>
        PersonaEditorHost.Content = editor;

    internal void ClearPersonaEditor(PersonaEditorControl editor)
    {
        if (ReferenceEquals(PersonaEditorHost.Content, editor))
        {
            PersonaEditorHost.Content = null;
        }
    }

    private void CompendiumListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CompendiumSelectionChanged?.Invoke(sender, e);

    private void CompendiumListView_Tapped(object sender, TappedRoutedEventArgs e) =>
        CompendiumTapped?.Invoke(sender, e);

    private void CompendiumAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CompendiumAddSelectionChanged?.Invoke(sender, e);

    private void CompendiumRemoveButton_Click(object sender, RoutedEventArgs e) =>
        CompendiumRemoveClick?.Invoke(sender, e);

    private void CompendiumClearButton_Click(object sender, RoutedEventArgs e) =>
        CompendiumClearClick?.Invoke(sender, e);
}
