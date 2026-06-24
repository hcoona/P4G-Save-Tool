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
    ];

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

    private static string GetReferencedAssemblyName(string include)
    {
        string reference = include.Split(',', 2)[0];
        return string.Equals(Path.GetExtension(reference), ".csproj", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(reference)
            : reference;
    }
}
