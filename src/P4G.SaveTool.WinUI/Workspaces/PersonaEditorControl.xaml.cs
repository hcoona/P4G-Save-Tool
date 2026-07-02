using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

public sealed partial class PersonaEditorControl : UserControl
{
    public PersonaEditorControl()
    {
        InitializeComponent();
    }

    internal event SelectionChangedEventHandler? PersonaMemberSelectionChanged;
    internal event SelectionChangedEventHandler? PersonaSlotSelectionChanged;
    internal event SelectionChangedEventHandler? PersonaChoiceSelectionChanged;
    internal event SelectionChangedEventHandler? PersonaSkillSelectionChanged;
    internal event TextChangedEventHandler? PersonaDraftTextChanged;
    internal event RangeBaseValueChangedEventHandler? PersonaDraftValueChanged;
    internal event RangeBaseValueChangedEventHandler? PersonaLevelValueChanged;
    internal event RoutedEventHandler? PersonaCalculateFromLevelClick;

    internal Brush? DefaultLevelValueForeground => PersonaLevelValueTextBlock.Foreground;

    internal PartyMemberChoiceViewState? SelectedMember
    {
        get => PersonaMemberComboBox.SelectedItem as PartyMemberChoiceViewState;
        set => PersonaMemberComboBox.SelectedItem = value;
    }

    internal PersonaSlotViewState? SelectedSlot
    {
        get => PersonaSlotComboBox.SelectedItem as PersonaSlotViewState;
        set => PersonaSlotComboBox.SelectedItem = value;
    }

    internal PersonaChoiceViewState? SelectedPersona
    {
        get => PersonaChoiceComboBox.SelectedItem as PersonaChoiceViewState;
        set => PersonaChoiceComboBox.SelectedItem = value;
    }

    internal string ExperienceText
    {
        get => PersonaXpTextBox.Text ?? string.Empty;
        set => PersonaXpTextBox.Text = value;
    }

    internal double Level
    {
        get => PersonaLevelSlider.Value;
        set => SetLevel(value);
    }

    internal double Strength
    {
        get => PersonaStrengthSlider.Value;
        set => PersonaStrengthSlider.Value = value;
    }

    internal double Magic
    {
        get => PersonaMagicSlider.Value;
        set => PersonaMagicSlider.Value = value;
    }

    internal double Endurance
    {
        get => PersonaEnduranceSlider.Value;
        set => PersonaEnduranceSlider.Value = value;
    }

    internal double Agility
    {
        get => PersonaAgilitySlider.Value;
        set => PersonaAgilitySlider.Value = value;
    }

    internal double Luck
    {
        get => PersonaLuckSlider.Value;
        set => PersonaLuckSlider.Value = value;
    }

    internal SkillChoiceViewState? SelectedSkill1
    {
        get => PersonaSkillBox1.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox1.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill2
    {
        get => PersonaSkillBox2.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox2.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill3
    {
        get => PersonaSkillBox3.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox3.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill4
    {
        get => PersonaSkillBox4.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox4.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill5
    {
        get => PersonaSkillBox5.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox5.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill6
    {
        get => PersonaSkillBox6.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox6.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill7
    {
        get => PersonaSkillBox7.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox7.SelectedItem = value;
    }

    internal SkillChoiceViewState? SelectedSkill8
    {
        get => PersonaSkillBox8.SelectedItem as SkillChoiceViewState;
        set => PersonaSkillBox8.SelectedItem = value;
    }

    internal void SetItemsSources(
        object memberChoicesSource,
        object slotChoicesSource,
        object personaChoicesSource,
        object skill1ChoicesSource,
        object skill2ChoicesSource,
        object skill3ChoicesSource,
        object skill4ChoicesSource,
        object skill5ChoicesSource,
        object skill6ChoicesSource,
        object skill7ChoicesSource,
        object skill8ChoicesSource)
    {
        PersonaMemberComboBox.ItemsSource = memberChoicesSource;
        PersonaSlotComboBox.ItemsSource = slotChoicesSource;
        PersonaChoiceComboBox.ItemsSource = personaChoicesSource;
        PersonaSkillBox1.ItemsSource = skill1ChoicesSource;
        PersonaSkillBox2.ItemsSource = skill2ChoicesSource;
        PersonaSkillBox3.ItemsSource = skill3ChoicesSource;
        PersonaSkillBox4.ItemsSource = skill4ChoicesSource;
        PersonaSkillBox5.ItemsSource = skill5ChoicesSource;
        PersonaSkillBox6.ItemsSource = skill6ChoicesSource;
        PersonaSkillBox7.ItemsSource = skill7ChoicesSource;
        PersonaSkillBox8.ItemsSource = skill8ChoicesSource;
    }

    internal void SetPersonaEditorEnabled(bool isEnabled, bool canSelectSlot, bool canSelectPersona)
    {
        PersonaMemberComboBox.IsEnabled = isEnabled;
        PersonaSlotComboBox.IsEnabled = isEnabled && canSelectSlot;
        PersonaChoiceComboBox.IsEnabled = isEnabled && canSelectPersona;
        PersonaXpTextBox.IsEnabled = isEnabled;
        PersonaLevelSlider.IsEnabled = isEnabled;
        PersonaCalculateFromLevelButton.IsEnabled = isEnabled;
        PersonaStrengthSlider.IsEnabled = isEnabled;
        PersonaMagicSlider.IsEnabled = isEnabled;
        PersonaEnduranceSlider.IsEnabled = isEnabled;
        PersonaAgilitySlider.IsEnabled = isEnabled;
        PersonaLuckSlider.IsEnabled = isEnabled;
        PersonaSkillBox1.IsEnabled = isEnabled;
        PersonaSkillBox2.IsEnabled = isEnabled;
        PersonaSkillBox3.IsEnabled = isEnabled;
        PersonaSkillBox4.IsEnabled = isEnabled;
        PersonaSkillBox5.IsEnabled = isEnabled;
        PersonaSkillBox6.IsEnabled = isEnabled;
        PersonaSkillBox7.IsEnabled = isEnabled;
        PersonaSkillBox8.IsEnabled = isEnabled;
    }

    internal void ClearEditorFields()
    {
        SelectedMember = null;
        SelectedSlot = null;
        SelectedPersona = null;
        ExperienceText = string.Empty;
        Level = 0;
        Strength = 0;
        Magic = 0;
        Endurance = 0;
        Agility = 0;
        Luck = 0;
        SelectedSkill1 = null;
        SelectedSkill2 = null;
        SelectedSkill3 = null;
        SelectedSkill4 = null;
        SelectedSkill5 = null;
        SelectedSkill6 = null;
        SelectedSkill7 = null;
        SelectedSkill8 = null;
    }

    internal void SetLevelValueText(string text) =>
        PersonaLevelValueTextBlock.Text = text;

    internal void SetLevelValueForeground(Brush? foreground) =>
        PersonaLevelValueTextBlock.Foreground = foreground;

    private void SetLevel(double rawLevel) =>
        PersonaLevelSlider.Value = Math.Clamp(rawLevel, PersonaLevelSlider.Minimum, PersonaLevelSlider.Maximum);

    private void PersonaMemberComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PersonaMemberSelectionChanged?.Invoke(sender, e);

    private void PersonaSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PersonaSlotSelectionChanged?.Invoke(sender, e);

    private void PersonaChoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PersonaChoiceSelectionChanged?.Invoke(sender, e);

    private void PersonaDraftControl_Changed(object sender, TextChangedEventArgs e) =>
        PersonaDraftTextChanged?.Invoke(sender, e);

    private void PersonaDraftControl_Changed(object sender, RangeBaseValueChangedEventArgs e) =>
        PersonaDraftValueChanged?.Invoke(sender, e);

    private void PersonaLevelSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        PersonaLevelValueChanged?.Invoke(sender, e);

    private void PersonaCalculateFromLevelButton_Click(object sender, RoutedEventArgs e) =>
        PersonaCalculateFromLevelClick?.Invoke(sender, e);

    private void PersonaSkillBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PersonaSkillSelectionChanged?.Invoke(sender, e);
}
