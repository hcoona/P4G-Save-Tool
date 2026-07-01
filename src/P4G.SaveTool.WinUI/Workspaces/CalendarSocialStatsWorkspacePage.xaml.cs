using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

internal sealed class SocialStatSelectionChangedEventArgs(int statIndex) : EventArgs
{
    internal int StatIndex { get; } = statIndex;
}

internal sealed class CalendarDayTextChangedEventArgs(bool isNextDay, string text) : EventArgs
{
    internal bool IsNextDay { get; } = isNextDay;

    internal string Text { get; } = text;
}

internal sealed class CalendarPhaseSelectionChangedEventArgs(bool isNextPhase) : EventArgs
{
    internal bool IsNextPhase { get; } = isNextPhase;
}

public sealed partial class CalendarSocialStatsWorkspacePage : Page
{
    public CalendarSocialStatsWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    internal event EventHandler<SocialStatSelectionChangedEventArgs>? SocialStatSelectionChanged;
    internal event EventHandler<CalendarDayTextChangedEventArgs>? DayTextChanged;
    internal event EventHandler<CalendarPhaseSelectionChangedEventArgs>? PhaseSelectionChanged;

    internal string DayText
    {
        get => DayTextBox.Text ?? string.Empty;
        set => DayTextBox.Text = value;
    }

    internal string NextDayText
    {
        get => NextDayTextBox.Text ?? string.Empty;
        set => NextDayTextBox.Text = value;
    }

    internal SocialStatRankChoiceViewState? GetSocialStatSelectedRank(int statIndex) =>
        GetSocialStatComboBox(statIndex).SelectedItem as SocialStatRankChoiceViewState;

    internal CalendarPhaseChoiceViewState? GetCalendarPhaseSelectedChoice(bool isNextPhase) =>
        GetCalendarPhaseComboBox(isNextPhase).SelectedItem as CalendarPhaseChoiceViewState;

    internal void SetCalendarSocialStatsEnabled(bool isEnabled)
    {
        CourageComboBox.IsEnabled = isEnabled;
        KnowledgeComboBox.IsEnabled = isEnabled;
        ExpressionComboBox.IsEnabled = isEnabled;
        UnderstandingComboBox.IsEnabled = isEnabled;
        DiligenceComboBox.IsEnabled = isEnabled;
        DayTextBox.IsEnabled = isEnabled;
        PhaseComboBox.IsEnabled = isEnabled;
        NextDayTextBox.IsEnabled = isEnabled;
        NextPhaseComboBox.IsEnabled = isEnabled;
    }

    internal void SetSocialStatSelection(
        int statIndex,
        IReadOnlyList<SocialStatRankChoiceViewState> choices,
        SocialStatRankChoiceViewState selectedChoice)
    {
        ComboBox comboBox = GetSocialStatComboBox(statIndex);
        comboBox.Items.Clear();
        foreach (SocialStatRankChoiceViewState choice in choices)
        {
            comboBox.Items.Add(choice);
        }

        comboBox.SelectedItem = selectedChoice;
    }

    internal void ClearSocialStatChoices(int statIndex)
    {
        ComboBox comboBox = GetSocialStatComboBox(statIndex);
        comboBox.Items.Clear();
        comboBox.SelectedItem = null;
    }

    internal void SetCalendarPhaseSelection(
        bool isNextPhase,
        IReadOnlyList<CalendarPhaseChoiceViewState> choices,
        CalendarPhaseChoiceViewState selectedChoice)
    {
        ComboBox comboBox = GetCalendarPhaseComboBox(isNextPhase);
        comboBox.Items.Clear();
        foreach (CalendarPhaseChoiceViewState choice in choices)
        {
            comboBox.Items.Add(choice);
        }

        comboBox.SelectedItem = selectedChoice;
    }

    internal void ClearCalendarPhaseChoices(bool isNextPhase)
    {
        ComboBox comboBox = GetCalendarPhaseComboBox(isNextPhase);
        comboBox.Items.Clear();
        comboBox.SelectedItem = null;
    }

    private ComboBox GetSocialStatComboBox(int statIndex) =>
        statIndex switch
        {
            0 => CourageComboBox,
            1 => KnowledgeComboBox,
            2 => DiligenceComboBox,
            3 => UnderstandingComboBox,
            4 => ExpressionComboBox,
            _ => throw new ArgumentOutOfRangeException(nameof(statIndex), statIndex, "Unknown social stat index."),
        };

    private ComboBox GetCalendarPhaseComboBox(bool isNextPhase) =>
        isNextPhase ? NextPhaseComboBox : PhaseComboBox;

    private void SocialStatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int? statIndex = sender switch
        {
            _ when ReferenceEquals(sender, CourageComboBox) => 0,
            _ when ReferenceEquals(sender, KnowledgeComboBox) => 1,
            _ when ReferenceEquals(sender, ExpressionComboBox) => 4,
            _ when ReferenceEquals(sender, UnderstandingComboBox) => 3,
            _ when ReferenceEquals(sender, DiligenceComboBox) => 2,
            _ => null,
        };

        if (statIndex.HasValue)
        {
            SocialStatSelectionChanged?.Invoke(this, new SocialStatSelectionChangedEventArgs(statIndex.Value));
        }
    }

    private void DayTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        DayTextChanged?.Invoke(this, new CalendarDayTextChangedEventArgs(false, DayText));

    private void NextDayTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        DayTextChanged?.Invoke(this, new CalendarDayTextChangedEventArgs(true, NextDayText));

    private void PhaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PhaseSelectionChanged?.Invoke(this, new CalendarPhaseSelectionChangedEventArgs(false));

    private void NextPhaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PhaseSelectionChanged?.Invoke(this, new CalendarPhaseSelectionChangedEventArgs(true));
}
