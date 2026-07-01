using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace P4G.SaveTool.WinUI;

public sealed partial class BasicStatsWorkspacePage : Page
{
    public BasicStatsWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        DefaultMainCharacterLevelValueForeground = MainCharacterLevelValueTextBlock.Foreground;
    }

    internal event TextChangedEventHandler? FamilyNameTextChanged;
    internal event TextChangedEventHandler? GivenNameTextChanged;
    internal event TextChangedEventHandler? YenTextChanged;
    internal event RangeBaseValueChangedEventHandler? MainCharacterLevelValueChanged;
    internal event TextChangedEventHandler? MainCharacterTotalExperienceTextChanged;
    internal event RoutedEventHandler? MainCharacterCalculateFromLevelClick;

    internal string FamilyNameText
    {
        get => FamilyNameTextBox.Text ?? string.Empty;
        set => FamilyNameTextBox.Text = value;
    }

    internal string GivenNameText
    {
        get => GivenNameTextBox.Text ?? string.Empty;
        set => GivenNameTextBox.Text = value;
    }

    internal string YenText
    {
        get => YenTextBox.Text ?? string.Empty;
        set => YenTextBox.Text = value;
    }

    internal double MainCharacterLevelRawValue => MainCharacterLevelSlider.Value;

    internal string MainCharacterTotalExperienceText
    {
        get => MainCharacterTotalExperienceTextBox.Text ?? string.Empty;
        set => MainCharacterTotalExperienceTextBox.Text = value;
    }

    internal Brush? DefaultMainCharacterLevelValueForeground { get; }

    internal void SetBasicStatsEnabled(bool isEnabled)
    {
        FamilyNameTextBox.IsEnabled = isEnabled;
        GivenNameTextBox.IsEnabled = isEnabled;
        YenTextBox.IsEnabled = isEnabled;
        MainCharacterLevelSlider.IsEnabled = isEnabled;
        MainCharacterTotalExperienceTextBox.IsEnabled = isEnabled;
        MainCharacterCalculateFromLevelButton.IsEnabled = isEnabled;
    }

    internal void SetMainCharacterLevelRawValue(double rawLevel) =>
        MainCharacterLevelSlider.Value = Math.Clamp(rawLevel, MainCharacterLevelSlider.Minimum, MainCharacterLevelSlider.Maximum);

    internal void SetMainCharacterLevelValueText(string text) =>
        MainCharacterLevelValueTextBlock.Text = text;

    internal void SetMainCharacterLevelValueForeground(Brush? foreground) =>
        MainCharacterLevelValueTextBlock.Foreground = foreground;

    private void FamilyNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        FamilyNameTextChanged?.Invoke(sender, e);

    private void GivenNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        GivenNameTextChanged?.Invoke(sender, e);

    private void YenTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        YenTextChanged?.Invoke(sender, e);

    private void MainCharacterLevelSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        MainCharacterLevelValueChanged?.Invoke(sender, e);

    private void MainCharacterTotalExperienceTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        MainCharacterTotalExperienceTextChanged?.Invoke(sender, e);

    private void MainCharacterCalculateFromLevelButton_Click(object sender, RoutedEventArgs e) =>
        MainCharacterCalculateFromLevelClick?.Invoke(sender, e);
}
