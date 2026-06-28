using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using P4G.SaveTool.Application;
using P4G.SaveTool.Presentation;
using Xunit;
using Xunit.Sdk;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class NativeAotUiSmokeTests
{
    private const string RunSmokeEnvVar = "P4G_RUN_NATIVEAOT_UIA_SMOKE";
    private const string ExePathEnvVar = "P4G_NATIVEAOT_UIA_SMOKE_EXE";
    private const string SavePathEnvVar = "P4G_NATIVEAOT_UIA_SMOKE_SAVE";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    [NativeAotUiSmokeFact]
    public async Task NativeAotStartupShowsVisibleMainWindow()
    {
        string exePath = ResolvePublishedExePath();
        using Process process = StartNativeAotProcess(exePath, arguments: null);

        try
        {
            UiSnapshot snapshot = await RunOnMtaThreadAsync(() => WaitForWindowSnapshot(process, ExpectedState.CreateNoSave()));

            Assert.False(process.HasExited);
            Assert.False(snapshot.WindowIsOffscreen);
            Assert.True(snapshot.WindowIsEnabled);
            Assert.Equal(ShellStateFormatter.ShellTitle, snapshot.WindowName);
            Assert.Equal(["No diagnostics."], snapshot.DiagnosticsItems);
        }
        finally
        {
            StopProcess(process);
        }
    }

    [NativeAotUiSmokeFact]
    public async Task NativeAotStartupFileLoadsSyntheticSave()
    {
        string exePath = ResolvePublishedExePath();
        string savePath = ResolveSyntheticSavePath(out bool deleteAfterUse);

        try
        {
            CreateSyntheticSave(savePath);
            using Process process = StartNativeAotProcess(exePath, savePath);

            try
            {
                UiSnapshot snapshot = await RunOnMtaThreadAsync(() =>
                    WaitForWindowSnapshot(
                        process,
                        ExpectedState.CreateLoadedSave(
                            savePath,
                            $"{ShellStateFormatter.ShellTitle} - {Path.GetFileName(savePath)}")));

                Assert.False(process.HasExited);
                Assert.False(snapshot.WindowIsOffscreen);
                Assert.True(snapshot.WindowIsEnabled);
                Assert.Equal($"{ShellStateFormatter.ShellTitle} - {Path.GetFileName(savePath)}", snapshot.WindowName);
                Assert.Equal(savePath, snapshot.FilePathText);
                Assert.Equal(string.Empty, snapshot.FamilyNameText);
                Assert.Equal(string.Empty, snapshot.GivenNameText);
                Assert.Equal("0", snapshot.YenText);
                Assert.Contains("Has save: yes", snapshot.StateText, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(["No diagnostics."], snapshot.DiagnosticsItems);
                Assert.True(snapshot.InventoryListIsEnabled);
                Assert.True(snapshot.SocialLinkListIsEnabled);
                Assert.True(snapshot.CompendiumListIsEnabled);
                Assert.True(snapshot.PersonaMemberComboBoxIsEnabled);
                Assert.True(snapshot.EquipmentCharacterComboBoxIsEnabled);

                AutomationElement? window = FindMainWindow(process.Id);
                Assert.NotNull(window);
                Assert.False(GetElementByAutomationId(window!, "CourageComboBox").Current.IsOffscreen);
                Assert.False(GetElementByAutomationId(window!, "KnowledgeComboBox").Current.IsOffscreen);
                Assert.False(GetElementByAutomationId(window!, "DiagnosticsListView").Current.IsOffscreen);
            }
            finally
            {
                StopProcess(process);
            }
        }
        finally
        {
            if (deleteAfterUse && File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [NativeAotUiSmokeFact]
    public async Task NativeAotStartupFileDisplaysMultipleValidationDiagnostics()
    {
        string exePath = ResolvePublishedExePath();
        string savePath = ResolveSyntheticSavePath(out bool deleteAfterUse);

        try
        {
            CreateSyntheticSave(savePath);
            using Process process = StartNativeAotProcess(exePath, savePath);

            try
            {
                _ = await RunOnMtaThreadAsync(() =>
                    WaitForWindowSnapshot(
                        process,
                        ExpectedState.CreateLoadedSave(
                            savePath,
                            $"{ShellStateFormatter.ShellTitle} - {Path.GetFileName(savePath)}")));

                AutomationElement? window = FindMainWindow(process.Id);
                Assert.NotNull(window);
                _ = WaitForEnabledElement(window!, "ApplyButton");

                SetTextByAutomationId(window!, "YenTextBox", "invalid");
                SetTextByAutomationId(window!, "MainCharacterTotalExperienceTextBox", "invalid");
                SetTextByAutomationId(window!, "DayTextBox", "invalid");
                SetTextByAutomationId(window!, "NextDayTextBox", "invalid");
                InvokeByAutomationId(window!, "ApplyButton");

                List<string> diagnosticsItems = await RunOnMtaThreadAsync(() =>
                    WaitForListItemTexts(window!, "DiagnosticsListView", minimumItemCount: 4));

                Assert.True(diagnosticsItems.Count >= 4);
                Assert.Contains(diagnosticsItems, item => item.Contains("P4GWINUI006", StringComparison.Ordinal));
                Assert.Contains(diagnosticsItems, item => item.Contains("P4GWINUI028", StringComparison.Ordinal));
                Assert.Contains(diagnosticsItems, item => item.Contains("P4GWINUI018", StringComparison.Ordinal));
                Assert.Contains(diagnosticsItems, item => item.Contains("P4GWINUI020", StringComparison.Ordinal));
            }
            finally
            {
                StopProcess(process);
            }
        }
        finally
        {
            if (deleteAfterUse && File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    private static void CreateSyntheticSave(string savePath)
    {
        SaveEditorViewModel viewModel = new(new SaveApplicationService());
        Assert.True(viewModel.CreateBlankSave().Succeeded);

        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        Assert.True(writeResult.Succeeded);
        Assert.NotNull(writeResult.Bytes);

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        File.WriteAllBytes(savePath, writeResult.Bytes!);
    }

    private static UiSnapshot WaitForWindowSnapshot(Process process, ExpectedState expectedState)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < StartupTimeout)
        {
            if (process.HasExited)
            {
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                throw new XunitException(
                    $"NativeAOT process exited early with code {process.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");
            }

            AutomationElement? window = FindMainWindow(process.Id);
            if (window is not null)
            {
                try
                {
                    UiSnapshot snapshot = ReadSnapshot(window);
                    if (expectedState.IsSatisfiedBy(snapshot))
                    {
                        return snapshot;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // The UI tree can briefly recycle while the startup file is still loading.
                }
                catch (XunitException)
                {
                    // Missing child elements are transient while NativeAOT is still materializing the UI tree.
                }
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException($"Timed out waiting for NativeAOT UI after {StartupTimeout.TotalSeconds} seconds.");
    }

    private static AutomationElement? FindMainWindow(int processId)
    {
        Condition processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        Condition windowCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
        Condition condition = new AndCondition(processCondition, windowCondition);
        return AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
    }

    private static UiSnapshot ReadSnapshot(AutomationElement window)
    {
        return new UiSnapshot(
            WindowName: window.Current.Name,
            WindowIsOffscreen: window.Current.IsOffscreen,
            WindowIsEnabled: window.Current.IsEnabled,
            FilePathText: GetTextByAutomationId(window, "FilePathTextBlock"),
            StateText: GetTextByAutomationId(window, "StateTextBlock"),
            FamilyNameText: GetValueByAutomationId(window, "FamilyNameTextBox"),
            GivenNameText: GetValueByAutomationId(window, "GivenNameTextBox"),
            YenText: GetValueByAutomationId(window, "YenTextBox"),
            DiagnosticsItems: GetListItemTextsByAutomationId(window, "DiagnosticsListView"),
            InventoryListIsEnabled: GetElementByAutomationId(window, "InventoryListView").Current.IsEnabled,
            SocialLinkListIsEnabled: GetElementByAutomationId(window, "SocialLinkListView").Current.IsEnabled,
            CompendiumListIsEnabled: GetElementByAutomationId(window, "CompendiumListView").Current.IsEnabled,
            PersonaMemberComboBoxIsEnabled: GetElementByAutomationId(window, "PersonaMemberComboBox").Current.IsEnabled,
            EquipmentCharacterComboBoxIsEnabled: GetElementByAutomationId(window, "EquipmentCharacterComboBox").Current.IsEnabled);
    }

    private static AutomationElement GetElementByAutomationId(AutomationElement root, string automationId)
    {
        AutomationElement? element = root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
        return element ?? throw new XunitException($"Could not find automation id '{automationId}'.");
    }

    private static string GetTextByAutomationId(AutomationElement root, string automationId)
    {
        AutomationElement element = GetElementByAutomationId(root, automationId);
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object? valuePatternObject) &&
            valuePatternObject is ValuePattern valuePattern)
        {
            return valuePattern.Current.Value ?? string.Empty;
        }

        if (element.TryGetCurrentPattern(TextPattern.Pattern, out object? textPatternObject) &&
            textPatternObject is TextPattern textPattern)
        {
            return textPattern.DocumentRange.GetText(-1)?.Trim() ?? string.Empty;
        }

        return element.Current.Name ?? string.Empty;
    }

    private static string GetValueByAutomationId(AutomationElement root, string automationId)
    {
        AutomationElement element = GetElementByAutomationId(root, automationId);
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object? valuePatternObject) &&
            valuePatternObject is ValuePattern valuePattern)
        {
            return valuePattern.Current.Value ?? string.Empty;
        }

        return element.Current.Name ?? string.Empty;
    }

    private static List<string> GetListItemTextsByAutomationId(AutomationElement root, string automationId)
    {
        AutomationElement list = GetElementByAutomationId(root, automationId);
        AutomationElementCollection listItems = list.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));

        List<string> itemTexts = [];
        foreach (AutomationElement listItem in listItems)
        {
            string itemText = listItem.Current.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(itemText))
            {
                itemTexts.Add(itemText);
            }
        }

        return itemTexts;
    }

    private static List<string> WaitForListItemTexts(AutomationElement root, string automationId, int minimumItemCount)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < StartupTimeout)
        {
            try
            {
                List<string> itemTexts = GetListItemTextsByAutomationId(root, automationId);
                if (itemTexts.Count >= minimumItemCount)
                {
                    return itemTexts;
                }
            }
            catch (ElementNotAvailableException)
            {
                // The target control can briefly recycle while the UI tree is still loading.
            }
            catch (XunitException)
            {
                // Missing child elements are transient while the target control is still loading.
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException($"Timed out waiting for at least {minimumItemCount} diagnostics items.");
    }

    private static void SetTextByAutomationId(AutomationElement root, string automationId, string value)
    {
        AutomationElement element = GetElementByAutomationId(root, automationId);
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object? valuePatternObject) ||
            valuePatternObject is not ValuePattern valuePattern)
        {
            throw new XunitException($"Could not set value for automation id '{automationId}'.");
        }

        valuePattern.SetValue(value);
    }

    private static void InvokeByAutomationId(AutomationElement root, string automationId)
    {
        AutomationElement element = GetElementByAutomationId(root, automationId);
        if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out object? invokePatternObject) ||
            invokePatternObject is not InvokePattern invokePattern)
        {
            throw new XunitException($"Could not invoke automation id '{automationId}'.");
        }

        invokePattern.Invoke();
    }

    private static AutomationElement WaitForEnabledElement(AutomationElement root, string automationId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < StartupTimeout)
        {
            try
            {
                AutomationElement element = GetElementByAutomationId(root, automationId);
                if (element.Current.IsEnabled)
                {
                    return element;
                }
            }
            catch (ElementNotAvailableException)
            {
                // The target control can briefly recycle while the UI tree is still loading.
            }
            catch (XunitException)
            {
                // Missing child elements are transient while the target control is still loading.
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException($"Timed out waiting for enabled automation id '{automationId}'.");
    }

    private static Process StartNativeAotProcess(string exePath, string? arguments)
    {
        ProcessStartInfo startInfo = new(exePath)
        {
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            startInfo.ArgumentList.Add(arguments);
        }

        Process process = new()
        {
            StartInfo = startInfo,
        };

        Assert.True(process.Start(), $"Could not start {exePath}.");
        return process;
    }

    private static string ResolvePublishedExePath()
    {
        string? envPath = Environment.GetEnvironmentVariable(ExePathEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            if (File.Exists(envPath))
            {
                return envPath;
            }

            throw new XunitException($"{ExePathEnvVar} points to a missing file: {envPath}");
        }

        string defaultPath = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64",
            "P4G.SaveTool.WinUI.exe");

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        throw new XunitException(
            $"Publish the NativeAOT app first, or set {ExePathEnvVar} to the published exe path.");
    }

    private static string ResolveSyntheticSavePath(out bool deleteAfterUse)
    {
        string? envPath = Environment.GetEnvironmentVariable(SavePathEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            deleteAfterUse = false;
            return envPath;
        }

        deleteAfterUse = true;
        return Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "smoke",
            "nativeaot-uia-smoke-save.bin");
    }

    private static Task<T> RunOnMtaThreadAsync<T>(Func<T> action)
    {
        TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return tcs.Task;
    }

    private static void StopProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        try
        {
            process.WaitForExit(10_000);
        }
        catch
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "P4G.SaveTool.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find P4G.SaveTool.sln from {AppContext.BaseDirectory}.");
    }

    private sealed record ExpectedState(
        string FilePathText,
        string FamilyNameText,
        string GivenNameText,
        string YenText,
        string? WindowName = null,
        string? StateText = null,
        IReadOnlyList<string>? DiagnosticsItems = null,
        bool InventoryListIsEnabled = true,
        bool SocialLinkListIsEnabled = true,
        bool CompendiumListIsEnabled = true,
        bool PersonaMemberComboBoxIsEnabled = true,
        bool EquipmentCharacterComboBoxIsEnabled = true)
    {
        public static ExpectedState CreateNoSave() =>
            new(
                ShellStateFormatter.GetFilePathText(null),
                string.Empty,
                string.Empty,
                string.Empty,
                WindowName: ShellStateFormatter.ShellTitle,
                StateText: ShellStateFormatter.GetStatusText(false, false, false),
                DiagnosticsItems: ["No diagnostics."],
                InventoryListIsEnabled: false,
                SocialLinkListIsEnabled: false,
                CompendiumListIsEnabled: false,
                PersonaMemberComboBoxIsEnabled: false,
                EquipmentCharacterComboBoxIsEnabled: false);

        public static ExpectedState CreateLoadedSave(string filePathText, string windowName) =>
            new(
                filePathText,
                string.Empty,
                string.Empty,
                "0",
                WindowName: windowName,
                StateText: ShellStateFormatter.GetStatusText(true, false, true),
                DiagnosticsItems: ["No diagnostics."]);

        public bool IsSatisfiedBy(UiSnapshot snapshot) =>
            !snapshot.WindowIsOffscreen &&
            snapshot.WindowIsEnabled &&
            string.Equals(snapshot.FilePathText, FilePathText, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(snapshot.FamilyNameText, FamilyNameText, StringComparison.Ordinal) &&
            string.Equals(snapshot.GivenNameText, GivenNameText, StringComparison.Ordinal) &&
            string.Equals(snapshot.YenText, YenText, StringComparison.Ordinal) &&
            (WindowName is null || string.Equals(snapshot.WindowName, WindowName, StringComparison.Ordinal)) &&
            (StateText is null || string.Equals(snapshot.StateText, StateText, StringComparison.Ordinal)) &&
            (DiagnosticsItems is null || snapshot.DiagnosticsItems.SequenceEqual(DiagnosticsItems)) &&
            snapshot.InventoryListIsEnabled == InventoryListIsEnabled &&
            snapshot.SocialLinkListIsEnabled == SocialLinkListIsEnabled &&
            snapshot.CompendiumListIsEnabled == CompendiumListIsEnabled &&
            snapshot.PersonaMemberComboBoxIsEnabled == PersonaMemberComboBoxIsEnabled &&
            snapshot.EquipmentCharacterComboBoxIsEnabled == EquipmentCharacterComboBoxIsEnabled;
    }

    private sealed record UiSnapshot(
        string WindowName,
        bool WindowIsOffscreen,
        bool WindowIsEnabled,
        string FilePathText,
        string StateText,
        string FamilyNameText,
        string GivenNameText,
        string YenText,
        IReadOnlyList<string> DiagnosticsItems,
        bool InventoryListIsEnabled,
        bool SocialLinkListIsEnabled,
        bool CompendiumListIsEnabled,
        bool PersonaMemberComboBoxIsEnabled,
        bool EquipmentCharacterComboBoxIsEnabled);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NativeAotUiSmokeFactAttribute : FactAttribute
{
    public NativeAotUiSmokeFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only NativeAOT UIA smoke test.";
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("P4G_RUN_NATIVEAOT_UIA_SMOKE"), "1", StringComparison.Ordinal))
        {
            Skip = "Set P4G_RUN_NATIVEAOT_UIA_SMOKE=1 to run this opt-in NativeAOT UIA smoke test.";
        }
    }
}



