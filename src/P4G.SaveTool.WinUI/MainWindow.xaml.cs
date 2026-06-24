using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using P4G.SaveTool.Application;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace P4G.SaveTool.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly SaveEditorViewModel viewModel;
    private IReadOnlyList<SaveDiagnostic>? uiDiagnosticsOverride;
    private string? currentFilePath;
    private bool isBusy;

    public MainWindow()
    {
        InitializeComponent();

        viewModel = new SaveEditorViewModel(new SaveApplicationService());
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RefreshFromViewModel();
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(OpenSaveFileAsync);

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ApplyEditorFields();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(() => SaveAsync(forcePicker: false));

    private async void SaveAsButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync(() => SaveAsync(forcePicker: true));

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshFromViewModel();

    private async Task RunBusyAsync(Func<Task> operation)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        UpdateShellState();
        try
        {
            await operation();
        }
        finally
        {
            isBusy = false;
            RefreshFromViewModel();
        }
    }

    private async Task OpenSaveFileAsync()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".bin");
        picker.FileTypeFilter.Add(".sav");
        picker.FileTypeFilter.Add(".dat");
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(file.Path))
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI001", "The selected file does not expose a local path.", "Open")]);
            return;
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(file.Path);
            uiDiagnosticsOverride = null;
            SaveEditorOperationResult result = viewModel.OpenSave(bytes);
            if (result.Succeeded)
            {
                currentFilePath = file.Path;
            }

            RefreshFromViewModel();
            if (!result.Succeeded)
            {
                await ShowMessageAsync("Open failed", FormatDiagnostics(result.Diagnostics));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IReadOnlyList<SaveDiagnostic> diagnostics =
            [
                CreateUiDiagnostic("P4GWINUI002", $"Could not read the selected file: {ex.Message}", "Open"),
            ];
            SetUiDiagnostics(diagnostics);
            await ShowMessageAsync("Open failed", FormatDiagnostics(diagnostics));
        }
    }

    private bool ApplyEditorFields()
    {
        if (!viewModel.HasSave)
        {
            SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI003", "Open a save before applying edits.", "Edit")]);
            return false;
        }

        if (!TryReadEditorValues(
            out string familyName,
            out string givenName,
            out uint yen,
            out IReadOnlyList<byte> partyMemberValues,
            out IReadOnlyList<SaveDiagnostic> diagnostics))
        {
            SetUiDiagnostics(diagnostics);
            return false;
        }

        uiDiagnosticsOverride = null;
        SaveEditorOperationResult result = viewModel.ApplyEditorValues(familyName, givenName, yen, partyMemberValues);
        RefreshFromViewModel();
        return result.Succeeded;
    }

    private async Task SaveAsync(bool forcePicker)
    {
        if (!ApplyEditorFields())
        {
            return;
        }

        string? targetPath = forcePicker || string.IsNullOrWhiteSpace(currentFilePath)
            ? await PickSavePathAsync()
            : currentFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        uiDiagnosticsOverride = null;
        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        RefreshFromViewModel();
        if (!writeResult.Succeeded || writeResult.Bytes is null || writeResult.OperationToken is null)
        {
            await ShowMessageAsync("Save failed", FormatDiagnostics(writeResult.Diagnostics));
            return;
        }

        SaveEditorWriteToken operationToken = writeResult.OperationToken.Value;
        try
        {
            await SafeFilePersistence.ReplaceFileAsync(targetPath, writeResult.Bytes);
        }
        catch (Exception ex) when (IsPersistenceException(ex))
        {
            IReadOnlyList<SaveDiagnostic> diagnostics =
            [
                CreateUiDiagnostic("P4GWINUI004", $"Could not write the save file: {ex.Message}", "Persistence"),
            ];
            SaveEditorOperationResult reportResult = viewModel.ReportSaveFailed(operationToken, diagnostics);
            RefreshFromViewModel();
            await ShowMessageAsync("Save failed", FormatDiagnostics(reportResult.Diagnostics));
            return;
        }

        currentFilePath = targetPath;
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);
        RefreshFromViewModel();
        if (!acknowledgeResult.Succeeded)
        {
            await ShowMessageAsync("Save acknowledgement failed", FormatDiagnostics(acknowledgeResult.Diagnostics));
        }
    }

    private async Task<string?> PickSavePathAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = string.IsNullOrWhiteSpace(currentFilePath)
                ? "data0001"
                : Path.GetFileNameWithoutExtension(currentFilePath),
        };
        picker.FileTypeChoices.Add("Persona 4 Golden save", new List<string> { ".bin" });
        picker.FileTypeChoices.Add("Save file", new List<string> { ".sav" });
        picker.FileTypeChoices.Add("Data file", new List<string> { ".dat" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(file.Path))
        {
            return file.Path;
        }

        SetUiDiagnostics([CreateUiDiagnostic("P4GWINUI005", "The selected save target does not expose a local path.", "Save")]);
        return null;
    }

    private bool TryReadEditorValues(
        out string familyName,
        out string givenName,
        out uint yen,
        out IReadOnlyList<byte> partyMemberValues,
        out IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        List<SaveDiagnostic> validationDiagnostics = [];
        List<byte> parsedPartyMemberValues = [];

        familyName = FamilyNameTextBox.Text ?? string.Empty;
        givenName = GivenNameTextBox.Text ?? string.Empty;

        if (uint.TryParse(YenTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedYen))
        {
            yen = parsedYen;
        }
        else
        {
            yen = 0;
            validationDiagnostics.Add(CreateUiDiagnostic("P4GWINUI006", "Yen must be an unsigned whole number.", "Yen"));
        }

        AddPartyMemberValue(PartySlot0TextBox, 0, parsedPartyMemberValues, validationDiagnostics);
        AddPartyMemberValue(PartySlot1TextBox, 1, parsedPartyMemberValues, validationDiagnostics);
        AddPartyMemberValue(PartySlot2TextBox, 2, parsedPartyMemberValues, validationDiagnostics);

        partyMemberValues = validationDiagnostics.Count == 0 ? parsedPartyMemberValues : [];
        diagnostics = validationDiagnostics;
        return validationDiagnostics.Count == 0;
    }

    private static void AddPartyMemberValue(
        TextBox textBox,
        int slotIndex,
        List<byte> partyMemberValues,
        List<SaveDiagnostic> diagnostics)
    {
        if (byte.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte memberValue))
        {
            partyMemberValues.Add(memberValue);
            return;
        }

        diagnostics.Add(CreateUiDiagnostic(
            "P4GWINUI007",
            $"Party slot {slotIndex + 1} must be a whole number from 0 to 255.",
            $"PartyMembers[{slotIndex}]"));
    }

    private void RefreshFromViewModel()
    {
        FamilyNameTextBox.Text = viewModel.FamilyName;
        GivenNameTextBox.Text = viewModel.GivenName;
        YenTextBox.Text = viewModel.HasSave ? viewModel.Yen.ToString(CultureInfo.InvariantCulture) : string.Empty;
        PartySlot0TextBox.Text = GetPartyMemberValue(0);
        PartySlot1TextBox.Text = GetPartyMemberValue(1);
        PartySlot2TextBox.Text = GetPartyMemberValue(2);
        PersonaSummaryTextBox.Text = BuildPersonaSummary();
        DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);
        UpdateShellState();
    }

    private void UpdateShellState()
    {
        bool canEdit = viewModel.HasSave && !isBusy;
        bool canSave = canEdit && viewModel.CanWrite;

        OpenButton.IsEnabled = !isBusy;
        ApplyButton.IsEnabled = canEdit;
        SaveButton.IsEnabled = canSave;
        SaveAsButton.IsEnabled = canSave;
        FamilyNameTextBox.IsEnabled = canEdit;
        GivenNameTextBox.IsEnabled = canEdit;
        YenTextBox.IsEnabled = canEdit;
        PartySlot0TextBox.IsEnabled = canEdit;
        PartySlot1TextBox.IsEnabled = canEdit;
        PartySlot2TextBox.IsEnabled = canEdit;

        FilePathTextBlock.Text = string.IsNullOrWhiteSpace(currentFilePath)
            ? "No save file is open."
            : currentFilePath;
        StateTextBlock.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Has save: {FormatBoolean(viewModel.HasSave)} | Dirty: {FormatBoolean(viewModel.IsDirty)} | Can write: {FormatBoolean(viewModel.CanWrite)}");
    }

    private string GetPartyMemberValue(int slotIndex) =>
        viewModel.PartyMembers.Count > slotIndex
            ? viewModel.PartyMembers[slotIndex].MemberValue.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    private string BuildPersonaSummary()
    {
        if (!viewModel.HasSave)
        {
            return "Open a save to inspect persona slot projections.";
        }

        List<string> lines =
        [
            $"Protagonist slots: {viewModel.ProtagonistPersonaSlots.Count}",
            $"Party persona slots: {viewModel.PartyPersonaSlots.Count}",
            $"Compendium slots: {viewModel.CompendiumPersonaSlots.Count}",
            string.Empty,
            "First protagonist slots:",
        ];

        lines.AddRange(viewModel.ProtagonistPersonaSlots.Take(8).Select(FormatPersonaSlot));
        return string.Join(Environment.NewLine, lines);
    }

    private void DisplayDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        DiagnosticsListView.ItemsSource = diagnostics.Count == 0
            ? new[] { "No diagnostics." }
            : diagnostics.Select(FormatDiagnostic).ToArray();
    }

    private void SetUiDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics)
    {
        uiDiagnosticsOverride = diagnostics;
        DisplayDiagnostics(diagnostics);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private static SaveDiagnostic CreateUiDiagnostic(string code, string message, string target) =>
        new(DiagnosticSeverity.Error, code, message, target);

    private static bool IsPersistenceException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string FormatPersonaSlot(PersonaSlotViewState slot) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Slot {slot.SlotIndex}: exists={slot.Exists}, id={slot.PersonaId}, level={slot.Level}, exp={slot.TotalExperience}");

    private static string FormatDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "No diagnostics were reported."
            : string.Join(Environment.NewLine, diagnostics.Select(FormatDiagnostic));

    private static string FormatDiagnostic(SaveDiagnostic diagnostic) =>
        string.IsNullOrWhiteSpace(diagnostic.Target)
            ? string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}")
            : string.Create(CultureInfo.InvariantCulture, $"{diagnostic.Severity} {diagnostic.Code} [{diagnostic.Target}]: {diagnostic.Message}");

    private static string FormatBoolean(bool value) =>
        value ? "yes" : "no";
}
