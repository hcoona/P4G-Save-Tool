using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class WinUIArchitectureTests
{
    private static readonly string[] ForbiddenBoundaryAssemblyNames =
    [
        "P4G.SaveTool.Domain",
        "P4G.SaveTool.SaveFormat",
        "P4G.SaveTool.Catalog",
    ];

    private static readonly string[] AotCompatibleProductionProjects =
    [
        "P4G.SaveTool.Domain",
        "P4G.SaveTool.Catalog",
        "P4G.SaveTool.Contracts",
        "P4G.SaveTool.SaveFormat",
        "P4G.SaveTool.Application",
        "P4G.SaveTool.Presentation",
    ];

    private static readonly TimeSpan MsBuildEvaluationTimeout = TimeSpan.FromSeconds(60);
    private static readonly Version MinimumCsWinRTAotVersion = new(2, 1, 1);

    private const string CsWinRTPackageName = "Microsoft.Windows.CsWinRT";
    private const string CsWinRTWindowsMetadataPackageName = "Microsoft.Windows.SDK.CPP";
    private const string CsWinRTWindowsMetadataPath = @"$(NuGetPackageRoot)\microsoft.windows.sdk.cpp\$(CsWinRTWindowsMetadataPackageVersion)\c";
    private const string RequiredCsWinRTWindowsMetadataPackageVersion = "10.0.22000.196";
    private const string RequiredCsWinRTWindowsMetadataPlatformVersion = "10.0.22000.0";
    private const string RequiredWindowsSdkPackageVersion = "10.0.22000.57";

    [Fact]
    public void WinUISourceDoesNotReferenceDomainOrSaveFormat()
    {
        string sourceRoot = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string[] sourceFiles = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .ToArray();

        Assert.NotEmpty(sourceFiles);
        foreach (string sourceFile in sourceFiles)
        {
            string content = File.ReadAllText(sourceFile);
            foreach (string forbiddenReference in ForbiddenBoundaryAssemblyNames)
            {
                Assert.False(
                    content.Contains(forbiddenReference, StringComparison.Ordinal),
                    $"{sourceFile} must not reference {forbiddenReference}.");
            }
        }
    }

    [Fact]
    public void MainWindowSourceDoesNotReferenceCatalogTypesOrLegacyInventoryIds()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile);

        Assert.DoesNotContain("P4G.SaveTool.Catalog", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemCategoryId", content, StringComparison.Ordinal);
        Assert.DoesNotContain("1792", content, StringComparison.Ordinal);
    }

    [Fact]
    public void InventorySelectionHandlersRefreshShellStateAfterSelectionChanges()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        AssertHandlerRefreshesShellState(content, "InventoryListView_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "InventoryCategoryComboBox_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "InventoryItemComboBox_SelectionChanged", "RefreshInventoryState();");
        AssertHandlerRefreshesShellState(content, "EquipmentCharacterComboBox_SelectionChanged", "RefreshEquipmentState();");
        Assert.Contains("ApplyEquipmentSelection(EquipmentWeaponComboBox", content, StringComparison.Ordinal);
        Assert.Contains("ApplyEquipmentSelection(EquipmentArmorComboBox", content, StringComparison.Ordinal);
        Assert.Contains("ApplyEquipmentSelection(EquipmentAccessoryComboBox", content, StringComparison.Ordinal);
        Assert.Contains("ApplyEquipmentSelection(EquipmentCostumeComboBox", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowEquipmentEditsRefreshOnlyEquipmentState()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void ApplyEquipmentSelection(",
            "private bool TryReadInventoryQuantity(");

        Assert.Contains("RefreshEquipmentState();", methodBody, StringComparison.Ordinal);
        Assert.Contains("DisplayDiagnostics(uiDiagnosticsOverride ?? viewModel.Diagnostics);", methodBody, StringComparison.Ordinal);
        Assert.Contains("UpdateShellState();", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFromViewModel();", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowZeroQuantityInventoryUpdatesSuppressAutoSelectLikeDelete()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);
        string methodBody = GetSection(
            content,
            "private void InventoryAddUpdateButton_Click(",
            "private void InventoryDeleteButton_Click(");

        Assert.Contains("if (quantity == 0)", methodBody, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowAutoSelectsInventoryEntryOnOpenAndSuppressesItAfterDelete()
    {
        string sourceFile = Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml.cs");
        string content = File.ReadAllText(sourceFile).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("inventorySelectionState.Reset();", content, StringComparison.Ordinal);
        Assert.Contains("autoSelectInventoryEntryAfterOpen = true;", content, StringComparison.Ordinal);
        Assert.Contains("ShouldAutoSelectFirstEntry(", content, StringComparison.Ordinal);
        Assert.Contains("selectedEntry = viewModel.InventoryEntries[lastInventoryEntryIndex];", content, StringComparison.Ordinal);
        Assert.Contains("int lastInventoryEntryIndex = viewModel.InventoryEntries.Count - 1;", content, StringComparison.Ordinal);
        Assert.Contains("inventorySelectionState.DisableAutoSelectAfterDelete();", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXamlExposesInventoryEditorSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"InventoryListView\"", xaml);
        Assert.Contains("x:Name=\"InventoryCategoryComboBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryItemComboBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryQuantityTextBox\"", xaml);
        Assert.Contains("x:Name=\"InventoryAddUpdateButton\"", xaml);
        Assert.Contains("x:Name=\"InventoryDeleteButton\"", xaml);
        Assert.Contains("Text=\"{Binding DisplayName}\"", xaml);
        Assert.Contains("x:Name=\"EquipmentCharacterComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentWeaponComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentArmorComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentAccessoryComboBox\"", xaml);
        Assert.Contains("x:Name=\"EquipmentCostumeComboBox\"", xaml);
    }

    [Fact]
    public void WinUIProjectDoesNotDirectlyReferenceDomainOrSaveFormat()
    {
        string projectFile = Path.Combine(
            FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"),
            "P4G.SaveTool.WinUI.csproj");
        XDocument project = XDocument.Load(projectFile);

        string[] directReferences = project
            .Descendants()
            .Where(static element => element.Name.LocalName is "ProjectReference" or "Reference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .Select(GetReferencedAssemblyName)
            .ToArray();

        Assert.NotEmpty(directReferences);
        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(forbiddenAssemblyName, directReferences);
        }
    }

    [Fact]
    public void WinUIAssemblyDoesNotDirectlyReferenceDomainOrSaveFormat()
    {
        string[] referencedAssemblies = typeof(MainWindow)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assemblyName => assemblyName.Name)
            .Where(static name => name is not null)
            .Select(static name => name!)
            .ToArray();

        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(forbiddenAssemblyName, referencedAssemblies);
        }
    }

    [Fact]
    public void PartyMemberViewStateDoesNotExposeDomainOrSaveFormatTypes()
    {
        Type[] exposedPropertyTypes = typeof(PartyMemberSlotViewState)
            .GetProperties()
            .Select(static property => property.PropertyType)
            .ToArray();

        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(
                exposedPropertyTypes,
                propertyType => propertyType.Assembly.GetName().Name == forbiddenAssemblyName);
        }
    }

    [Fact]
    public void EquipmentCharacterViewStateDoesNotExposeDomainOrSaveFormatTypes()
    {
        Type[] exposedPropertyTypes = typeof(EquipmentCharacterViewState)
            .GetProperties()
            .Select(static property => property.PropertyType)
            .ToArray();

        foreach (string forbiddenAssemblyName in ForbiddenBoundaryAssemblyNames)
        {
            Assert.DoesNotContain(
                exposedPropertyTypes,
                propertyType => propertyType.Assembly.GetName().Name == forbiddenAssemblyName);
        }
    }

    [Fact]
    public void WinUIProjectDeclaresUnpackagedNativeAotPublishSettings()
    {
        XDocument project = XDocument.Load(GetWinUIProjectFile());

        AssertProperty(project, "UseWinUI", "true");
        AssertProperty(project, "WindowsPackageType", "None");
        AssertProperty(project, "Platforms", "x64");
        AssertProperty(project, "RuntimeIdentifier", "win-x64");
        AssertProperty(project, "SelfContained", "true");
        AssertProperty(project, "WindowsAppSDKSelfContained", "true");
        AssertProperty(project, "PublishAot", "true");
        AssertProperty(project, "WindowsSdkPackageVersion", RequiredWindowsSdkPackageVersion);
        AssertProperty(project, "CsWinRTWindowsMetadataPackageVersion", RequiredCsWinRTWindowsMetadataPackageVersion);
        AssertProperty(project, "CsWinRTWindowsMetadata", CsWinRTWindowsMetadataPath);
        AssertNoTrueProperty(project, "PublishSingleFile");
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName == "RuntimeIdentifiers");
    }

    [Fact]
    public void WinUIProjectReferencesCsWinRTSourceGeneratorForNativeAot()
    {
        XDocument project = XDocument.Load(GetWinUIProjectFile());
        string[] directPackageReferences = GetPackageIncludes(project, "PackageReference");
        Assert.Contains(CsWinRTPackageName, directPackageReferences);
        Assert.Contains(CsWinRTWindowsMetadataPackageName, directPackageReferences);

        XDocument centralPackages = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"));
        string packageVersion = GetRequiredPackageVersion(centralPackages, CsWinRTPackageName);
        Version parsedVersion = ParsePackageVersion(packageVersion, CsWinRTPackageName);
        Assert.True(
            parsedVersion.CompareTo(MinimumCsWinRTAotVersion) >= 0,
            $"{CsWinRTPackageName} must be at least {MinimumCsWinRTAotVersion} for WinUI NativeAOT source generation.");
        Assert.Equal(
            RequiredCsWinRTWindowsMetadataPackageVersion,
            GetRequiredPackageVersion(centralPackages, CsWinRTWindowsMetadataPackageName));
    }

    [Fact]
    public void NativeAotPublishProfileDeclaresStableFolderPublishSettings()
    {
        string winUIProjectDirectory = FindRepositoryDirectory("src", "P4G.SaveTool.WinUI");
        string publishProfile = Path.Combine(
            winUIProjectDirectory,
            "Properties",
            "PublishProfiles",
            "nativeaot-win-x64.pubxml");
        XDocument profile = XDocument.Load(publishProfile);

        AssertProperty(profile, "Configuration", "Release");
        AssertProperty(profile, "Platform", "x64");
        AssertProperty(profile, "RuntimeIdentifier", "win-x64");
        AssertProperty(profile, "PublishAot", "true");
        AssertProperty(profile, "SelfContained", "true");
        AssertProperty(profile, "WindowsAppSDKSelfContained", "true");
        AssertNoTrueProperty(profile, "PublishSingleFile");

        string expectedPublishDirectory = NormalizeDirectoryPath(Path.Combine(
            winUIProjectDirectory,
            "..",
            "..",
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64"));
        string publishDirectory = EvaluatePublishDirectory(
            GetRequiredPropertyValue(profile, "PublishDir"),
            winUIProjectDirectory);
        string actualPublishDirectory = NormalizeDirectoryPath(publishDirectory);
        Assert.True(
            string.Equals(expectedPublishDirectory, actualPublishDirectory, StringComparison.OrdinalIgnoreCase),
            $"PublishDir must resolve to '{expectedPublishDirectory}' but resolved to '{actualPublishDirectory}'.");
    }

    [Fact]
    public void ProductionLibraryProjectsAreMarkedAotCompatible()
    {
        foreach (string projectName in AotCompatibleProductionProjects)
        {
            string projectFile = Path.Combine(
                FindRepositoryDirectory("src", projectName),
                $"{projectName}.csproj");
            XDocument project = XDocument.Load(projectFile);

            AssertProperty(project, "IsAotCompatible", "true");
        }
    }

    [Fact]
    public async Task NativeAotPublishProfileDefaultsEvaluateWithoutExplicitGlobals()
    {
        IReadOnlyDictionary<string, string> properties = await GetEvaluatedPropertiesAsync(
            GetWinUIProjectFile(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PublishProfile"] = "nativeaot-win-x64",
            },
            [
                "Configuration",
                "Platform",
                "RuntimeIdentifier",
                "WindowsPackageType",
                "SelfContained",
                "WindowsAppSDKSelfContained",
                "PublishAot",
                "PublishSingleFile",
                "PublishDir",
                "WindowsSdkPackageVersion",
                "CsWinRTWindowsMetadataPackageVersion",
                "CsWinRTWindowsMetadata",
                "NuGetPackageRoot",
            ]);
 
        AssertEvaluatedProperty(properties, "Configuration", "Release");
        AssertEvaluatedProperty(properties, "Platform", "x64");
        AssertEvaluatedProperty(properties, "RuntimeIdentifier", "win-x64");
        AssertEvaluatedProperty(properties, "WindowsPackageType", "None");
        AssertEvaluatedProperty(properties, "SelfContained", "true");
        AssertEvaluatedProperty(properties, "WindowsAppSDKSelfContained", "true");
        AssertEvaluatedProperty(properties, "PublishAot", "true");
        AssertEvaluatedFalseOrEmptyProperty(properties, "PublishSingleFile");
        AssertEvaluatedWindowsSdkMetadataProperties(properties);

        string expectedPublishDirectory = NormalizeDirectoryPath(Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64"));
        string actualPublishDirectory = NormalizeDirectoryPath(
            GetRequiredEvaluatedProperty(properties, "PublishDir"));
        Assert.True(
            string.Equals(expectedPublishDirectory, actualPublishDirectory, StringComparison.OrdinalIgnoreCase),
            $"PublishDir must evaluate to '{expectedPublishDirectory}' but evaluated to '{actualPublishDirectory}'.");
    }

    [Fact]
    public async Task NativeAotPublishProfileEvaluatesExpectedPublishSettings()
    {
        IReadOnlyDictionary<string, string> properties = await GetEvaluatedPropertiesAsync(
            GetWinUIProjectFile(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = "Release",
                ["Platform"] = "x64",
                ["RuntimeIdentifier"] = "win-x64",
                ["PublishProfile"] = "nativeaot-win-x64",
            },
            [
                "WindowsPackageType",
                "SelfContained",
                "WindowsAppSDKSelfContained",
                "PublishAot",
                "PublishSingleFile",
                "RuntimeIdentifier",
                "Platform",
                "PublishDir",
                "WindowsSdkPackageVersion",
                "CsWinRTWindowsMetadataPackageVersion",
                "CsWinRTWindowsMetadata",
                "NuGetPackageRoot",
            ]);

        AssertEvaluatedProperty(properties, "WindowsPackageType", "None");
        AssertEvaluatedProperty(properties, "SelfContained", "true");
        AssertEvaluatedProperty(properties, "WindowsAppSDKSelfContained", "true");
        AssertEvaluatedProperty(properties, "PublishAot", "true");
        AssertEvaluatedFalseOrEmptyProperty(properties, "PublishSingleFile");
        AssertEvaluatedProperty(properties, "RuntimeIdentifier", "win-x64");
        AssertEvaluatedProperty(properties, "Platform", "x64");
        AssertEvaluatedWindowsSdkMetadataProperties(properties);

        string expectedPublishDirectory = NormalizeDirectoryPath(Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "publish",
            "P4G.SaveTool.WinUI",
            "nativeaot-win-x64"));
        string actualPublishDirectory = NormalizeDirectoryPath(
            GetRequiredEvaluatedProperty(properties, "PublishDir"));
        Assert.True(
            string.Equals(expectedPublishDirectory, actualPublishDirectory, StringComparison.OrdinalIgnoreCase),
            $"PublishDir must evaluate to '{expectedPublishDirectory}' but evaluated to '{actualPublishDirectory}'.");
    }

    [Fact]
    public async Task ProductionLibraryProjectsEvaluateAotCompatible()
    {
        IReadOnlyDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = "Release",
            ["Platform"] = "x64",
            ["RuntimeIdentifier"] = "win-x64",
        };

        foreach (string projectName in AotCompatibleProductionProjects)
        {
            string projectFile = Path.Combine(
                FindRepositoryDirectory("src", projectName),
                $"{projectName}.csproj");
            IReadOnlyDictionary<string, string> properties = await GetEvaluatedPropertiesAsync(
                projectFile,
                globalProperties,
                ["IsAotCompatible"]);

            AssertEvaluatedProperty(properties, "IsAotCompatible", "true");
        }
    }

    private static string FindRepositoryDirectory(params string[] relativePathSegments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativePathSegments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find {Path.Combine(relativePathSegments)} from {AppContext.BaseDirectory}.");
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

        throw new DirectoryNotFoundException(
            $"Could not find P4G.SaveTool.sln from {AppContext.BaseDirectory}.");
    }

    private static string GetSection(string content, string startMarker, string endMarker)
    {
        int startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Could not find start marker '{startMarker}'.");

        int endIndex = content.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Could not find end marker '{endMarker}'.");

        return content.Substring(startIndex, endIndex - startIndex);
    }

    private static string GetReferencedAssemblyName(string include)
    {
        string reference = include.Split(',', 2)[0];
        return string.Equals(Path.GetExtension(reference), ".csproj", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(reference)
            : reference;
    }

    private static void AssertHandlerRefreshesShellState(string content, string methodName, string refreshMethodName)
    {
        string methodHeader = $"    private void {methodName}(object sender, SelectionChangedEventArgs e)";
        int methodStart = content.IndexOf(methodHeader, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"{methodName} was not found.");

        int nextMethodStart = content.IndexOf("\n    private ", methodStart + methodHeader.Length, StringComparison.Ordinal);
        string methodBody = nextMethodStart >= 0
            ? content.Substring(methodStart, nextMethodStart - methodStart)
            : content[methodStart..];

        int refreshIndex = methodBody.IndexOf(refreshMethodName, StringComparison.Ordinal);
        int shellIndex = refreshIndex >= 0
            ? methodBody.IndexOf("UpdateShellState();", refreshIndex, StringComparison.Ordinal)
            : -1;

        Assert.True(refreshIndex >= 0, $"{methodName} must refresh state.");
        Assert.True(shellIndex > refreshIndex, $"{methodName} must update shell state after refreshing state.");
    }

    private static string GetWinUIProjectFile()
    {
        return Path.Combine(
            FindRepositoryDirectory("src", "P4G.SaveTool.WinUI"),
            "P4G.SaveTool.WinUI.csproj");
    }

    private static void AssertProperty(XDocument project, string propertyName, string expectedValue)
    {
        string actualValue = GetRequiredPropertyValue(project, propertyName);
        Assert.Equal(expectedValue, actualValue);
    }

    private static void AssertNoTrueProperty(XDocument project, string propertyName)
    {
        string[] actualValues = GetPropertyValues(project, propertyName);
        Assert.True(
            actualValues.Length <= 1,
            $"{propertyName} must not be declared more than once. Found values: {FormatPropertyValues(actualValues)}.");
        Assert.False(
            actualValues.Any(static actualValue => string.Equals(actualValue, "true", StringComparison.OrdinalIgnoreCase)),
            $"{propertyName} must not be set to true.");
    }

    private static void AssertEvaluatedProperty(
        IReadOnlyDictionary<string, string> properties,
        string propertyName,
        string expectedValue)
    {
        string actualValue = GetRequiredEvaluatedProperty(properties, propertyName);
        Assert.Equal(expectedValue, actualValue);
    }

    private static void AssertEvaluatedFalseOrEmptyProperty(
        IReadOnlyDictionary<string, string> properties,
        string propertyName)
    {
        string actualValue = GetRequiredEvaluatedProperty(properties, propertyName);
        Assert.True(
            string.IsNullOrWhiteSpace(actualValue)
            || string.Equals(actualValue, "false", StringComparison.OrdinalIgnoreCase),
            $"{propertyName} must evaluate empty or false, but evaluated to '{actualValue}'.");
    }

    private static string GetRequiredPropertyValue(XDocument project, string propertyName)
    {
        string[] values = GetPropertyValues(project, propertyName);
        Assert.True(values.Length > 0, $"Expected {propertyName} to be set.");
        Assert.True(
            values.Length == 1,
            $"{propertyName} must be declared exactly once. Found values: {FormatPropertyValues(values)}.");
        return values[0];
    }

    private static string[] GetPropertyValues(XDocument project, string propertyName)
    {
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == propertyName)
            .Select(static element => element.Value.Trim())
            .ToArray();
    }

    private static string[] GetPackageIncludes(XDocument project, string itemName)
    {
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == itemName)
            .Select(static element => ((string?)element.Attribute("Include"))?.Trim())
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .ToArray();
    }

    private static string GetRequiredPackageVersion(XDocument centralPackages, string packageName)
    {
        string[] packageVersions = centralPackages
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion"
                && string.Equals((string?)element.Attribute("Include"), packageName, StringComparison.OrdinalIgnoreCase))
            .Select(static element => ((string?)element.Attribute("Version"))?.Trim())
            .Where(static version => !string.IsNullOrWhiteSpace(version))
            .Select(static version => version!)
            .ToArray();

        Assert.True(
            packageVersions.Length == 1,
            $"Expected exactly one central {packageName} version. Found values: {FormatPropertyValues(packageVersions)}.");
        return packageVersions[0];
    }

    private static Version ParsePackageVersion(string packageVersion, string packageName)
    {
        string stableVersion = packageVersion.Split('-', 2)[0];
        Assert.True(
            Version.TryParse(stableVersion, out Version? parsedVersion),
            $"Expected {packageName} version '{packageVersion}' to be a valid semantic version.");
        return parsedVersion;
    }

    private static string GetRequiredEvaluatedProperty(
        IReadOnlyDictionary<string, string> properties,
        string propertyName)
    {
        Assert.True(
            properties.TryGetValue(propertyName, out string? value),
            $"Expected MSBuild to return {propertyName}. Returned properties: {string.Join(", ", properties.Keys)}.");
        return value;
    }

    private static void AssertEvaluatedWindowsSdkMetadataProperties(
        IReadOnlyDictionary<string, string> properties)
    {
        AssertEvaluatedProperty(properties, "WindowsSdkPackageVersion", RequiredWindowsSdkPackageVersion);
        AssertEvaluatedProperty(
            properties,
            "CsWinRTWindowsMetadataPackageVersion",
            RequiredCsWinRTWindowsMetadataPackageVersion);

        string nugetPackageRoot = NormalizeDirectoryPath(GetRequiredEvaluatedProperty(properties, "NuGetPackageRoot"));
        string expectedMetadataRoot = NormalizeDirectoryPath(Path.Combine(
            nugetPackageRoot,
            "microsoft.windows.sdk.cpp",
            RequiredCsWinRTWindowsMetadataPackageVersion,
            "c"));
        string actualMetadataRoot = NormalizeDirectoryPath(GetRequiredEvaluatedProperty(
            properties,
            "CsWinRTWindowsMetadata"));

        Assert.False(
            actualMetadataRoot.Contains("$(", StringComparison.Ordinal),
            $"CsWinRTWindowsMetadata must be fully expanded but evaluated to '{actualMetadataRoot}'.");
        Assert.True(
            string.Equals(expectedMetadataRoot, actualMetadataRoot, StringComparison.OrdinalIgnoreCase),
            $"CsWinRTWindowsMetadata must evaluate to '{expectedMetadataRoot}' but evaluated to '{actualMetadataRoot}'.");
        Assert.DoesNotContain(
            Path.Combine("Windows Kits", "10"),
            actualMetadataRoot,
            StringComparison.OrdinalIgnoreCase);

        string platformMetadataFile = Path.Combine(
            actualMetadataRoot,
            "Platforms",
            "UAP",
            RequiredCsWinRTWindowsMetadataPlatformVersion,
            "Platform.xml");
        Assert.True(
            File.Exists(platformMetadataFile),
            $"CsWinRTWindowsMetadata must resolve to a NuGet Windows SDK package root containing {platformMetadataFile}.");
    }

    private static async Task<IReadOnlyDictionary<string, string>> GetEvaluatedPropertiesAsync(
        string projectFile,
        IReadOnlyDictionary<string, string> globalProperties,
        string[] propertyNames)
    {
        Assert.NotEmpty(propertyNames);

        ProcessStartInfo startInfo = new(GetDotNetExecutable())
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectFile);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-v:quiet");
        startInfo.ArgumentList.Add($"-getProperty:{string.Join(",", propertyNames)}");
        foreach (KeyValuePair<string, string> globalProperty in globalProperties)
        {
            startInfo.ArgumentList.Add($"-p:{globalProperty.Key}={globalProperty.Value}");
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        Task waitTask = process.WaitForExitAsync();
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(MsBuildEvaluationTimeout)).ConfigureAwait(false);
        if (completedTask != waitTask)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail(
                $"dotnet msbuild did not complete within {MsBuildEvaluationTimeout.TotalSeconds} seconds for {projectFile}.");
        }

        await waitTask.ConfigureAwait(false);
        string standardOutput = await standardOutputTask.ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);

        Assert.True(
            process.ExitCode == 0,
            $"dotnet msbuild failed for {projectFile} with exit code {process.ExitCode}."
            + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}"
            + $"{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");

        return ParseMsBuildProperties(standardOutput, propertyNames);
    }

    private static Dictionary<string, string> ParseMsBuildProperties(
        string standardOutput,
        string[] propertyNames)
    {
        string trimmedOutput = standardOutput.Trim();
        if (propertyNames.Length == 1)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [propertyNames[0]] = trimmedOutput,
            };
        }

        using JsonDocument document = JsonDocument.Parse(trimmedOutput);
        JsonElement propertiesElement = document.RootElement.GetProperty("Properties");
        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in propertiesElement.EnumerateObject())
        {
            properties[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return properties;
    }

    private static string GetDotNetExecutable()
    {
        string? dotNetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return string.IsNullOrWhiteSpace(dotNetHostPath)
            ? "dotnet"
            : dotNetHostPath;
    }

    private static string EvaluatePublishDirectory(string publishDirectory, string projectDirectory)
    {
        string expandedPublishDirectory = publishDirectory.Replace(
            "$(MSBuildProjectDirectory)",
            projectDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            expandedPublishDirectory.Contains("$(", StringComparison.Ordinal),
            $"PublishDir contains unevaluated MSBuild properties: {publishDirectory}");
        return expandedPublishDirectory;
    }

    private static string NormalizeDirectoryPath(string directoryPath)
    {
        string fullPath = Path.GetFullPath(directoryPath);
        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static string FormatPropertyValues(string[] values)
    {
        return values.Length == 0
            ? "<none>"
            : string.Join(", ", values.Select(static value => $"'{value}'"));
    }
}
