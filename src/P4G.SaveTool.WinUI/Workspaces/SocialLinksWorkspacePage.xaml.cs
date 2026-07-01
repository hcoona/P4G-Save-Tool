using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

public sealed partial class SocialLinksWorkspacePage : Page
{
    public SocialLinksWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    internal event SelectionChangedEventHandler? SocialLinkSelectionChanged;
    internal event SelectionChangedEventHandler? SocialLinkAddSelectionChanged;
    internal event TextChangedEventHandler? SocialLinkTextChanged;
    internal event RoutedEventHandler? SocialLinkDeleteClick;

    internal SocialLinkViewState? SelectedSocialLink
    {
        get => SocialLinkListView.SelectedItem as SocialLinkViewState;
        set => SocialLinkListView.SelectedItem = value;
    }

    internal SocialLinkChoiceViewState? SelectedSocialLinkAddChoice
    {
        get => SocialLinkAddComboBox.SelectedItem as SocialLinkChoiceViewState;
        set => SocialLinkAddComboBox.SelectedItem = value;
    }

    internal string SocialLinkLevelText
    {
        get => SocialLinkLevelTextBox.Text ?? string.Empty;
        set => SocialLinkLevelTextBox.Text = value;
    }

    internal string SocialLinkProgressText
    {
        get => SocialLinkProgressTextBox.Text ?? string.Empty;
        set => SocialLinkProgressTextBox.Text = value;
    }

    internal string SelectedSocialLinkDescription =>
        SocialLinkListView.SelectedItem?.ToString() ?? "the selected social link";

    internal void SetItemsSources(
        object socialLinksSource,
        object socialLinkChoicesSource)
    {
        SocialLinkListView.ItemsSource = socialLinksSource;
        SocialLinkAddComboBox.ItemsSource = socialLinkChoicesSource;
    }

    internal void SetSocialLinksEnabled(bool isEnabled, bool hasSelectedLink)
    {
        SocialLinkListView.IsEnabled = isEnabled;
        SocialLinkAddComboBox.IsEnabled = isEnabled;
        SocialLinkLevelTextBox.IsEnabled = isEnabled && hasSelectedLink;
        SocialLinkProgressTextBox.IsEnabled = isEnabled && hasSelectedLink;
        SocialLinkDeleteButton.IsEnabled = isEnabled && hasSelectedLink;
    }

    internal void SetEmptyStateVisible(bool isVisible) =>
        NoSocialLinksTextBlock.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

    private void SocialLinkListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SocialLinkSelectionChanged?.Invoke(sender, e);

    private void SocialLinkAddComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SocialLinkAddSelectionChanged?.Invoke(sender, e);

    private void SocialLinkTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        SocialLinkTextChanged?.Invoke(sender, e);

    private void SocialLinkDeleteButton_Click(object sender, RoutedEventArgs e) =>
        SocialLinkDeleteClick?.Invoke(sender, e);
}
