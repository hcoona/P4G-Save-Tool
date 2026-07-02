using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

public sealed partial class PartyPersonaWorkspacePage : Page
{
    public PartyPersonaWorkspacePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    internal event SelectionChangedEventHandler? PartySlot0SelectionChanged;
    internal event SelectionChangedEventHandler? PartySlot1SelectionChanged;
    internal event SelectionChangedEventHandler? PartySlot2SelectionChanged;

    internal PartyConfigurationChoiceViewState? SelectedPartySlot0
    {
        get => PartySlot0ComboBox.SelectedItem as PartyConfigurationChoiceViewState;
        set => PartySlot0ComboBox.SelectedItem = value;
    }

    internal PartyConfigurationChoiceViewState? SelectedPartySlot1
    {
        get => PartySlot1ComboBox.SelectedItem as PartyConfigurationChoiceViewState;
        set => PartySlot1ComboBox.SelectedItem = value;
    }

    internal PartyConfigurationChoiceViewState? SelectedPartySlot2
    {
        get => PartySlot2ComboBox.SelectedItem as PartyConfigurationChoiceViewState;
        set => PartySlot2ComboBox.SelectedItem = value;
    }

    internal void SetItemsSources(
        object partySlot0ChoicesSource,
        object partySlot1ChoicesSource,
        object partySlot2ChoicesSource)
    {
        PartySlot0ComboBox.ItemsSource = partySlot0ChoicesSource;
        PartySlot1ComboBox.ItemsSource = partySlot1ChoicesSource;
        PartySlot2ComboBox.ItemsSource = partySlot2ChoicesSource;
    }

    internal PartyConfigurationChoiceViewState? GetSelectedPartySlot(int slotIndex) =>
        slotIndex switch
        {
            0 => SelectedPartySlot0,
            1 => SelectedPartySlot1,
            2 => SelectedPartySlot2,
            _ => null,
        };

    internal void SetSelectedPartySlot(int slotIndex, PartyConfigurationChoiceViewState? selectedChoice)
    {
        switch (slotIndex)
        {
            case 0:
                SelectedPartySlot0 = selectedChoice;
                break;
            case 1:
                SelectedPartySlot1 = selectedChoice;
                break;
            case 2:
                SelectedPartySlot2 = selectedChoice;
                break;
        }
    }

    internal void SetPartyConfigurationEnabled(bool isEnabled)
    {
        PartySlot0ComboBox.IsEnabled = isEnabled;
        PartySlot1ComboBox.IsEnabled = isEnabled;
        PartySlot2ComboBox.IsEnabled = isEnabled;
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

    private void PartySlot0ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PartySlot0SelectionChanged?.Invoke(sender, e);

    private void PartySlot1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PartySlot1SelectionChanged?.Invoke(sender, e);

    private void PartySlot2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PartySlot2SelectionChanged?.Invoke(sender, e);
}
