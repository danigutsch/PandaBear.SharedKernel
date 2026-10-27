using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Guards the build wiring that makes the testing analyzers run in the assertion libraries.</summary>
public static class AssertionLibraryAnalyzerTests
{
    private const string OptInCondition = "'$(EnableSharedKernelTestingAnalyzers)' == 'true'";
    private const string PropsDirectory = "$(MSBuildThisFileDirectory)";

    [Theory]
    [InlineData("SharedKernel.Testing.Assertions")]
    [InlineData("SharedKernel.Testing.Http")]
    [InlineData("SharedKernel.Testing.Playwright")]
    public static void Assertion_libraries_enable_SKTEST017(string projectName)
    {
        // Arrange
        var projectDirectory = Path.Combine(
            SharedKernelPackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel", projectName);

        // Act
        var enablesTestingAnalyzers = SharedKernelPackagingConventionTestFiles.GetProperty(
            Path.Combine(projectDirectory, projectName + ".csproj"), "EnableSharedKernelTestingAnalyzers");
        var editorConfigLines = File.ReadAllLines(Path.Combine(projectDirectory, ".editorconfig"));

        // Assert
        enablesTestingAnalyzers.ShouldBe(
            "true", userMessage: "Assertion libraries reference the testing analyzers so that SKTEST017 runs in their build.");
        editorConfigLines.ShouldContain(
            "dotnet_diagnostic.SKTEST017.severity = warning",
            userMessage: "SKTEST017 is disabled by default, so assertion libraries enable it in their .editorconfig.");
    }

    [Theory]
    [InlineData("SharedKernel.Testing.Analyzers")]
    [InlineData("SharedKernel.Testing.CodeFixes")]
    public static void Source_props_reference_the_testing_analyzers_when_a_project_opts_in(string analyzerProjectName)
    {
        // Arrange
        var sourceDirectory = Path.Combine(SharedKernelPackagingConventionTestFiles.GetRepositoryRoot(), "src");
        var analyzerProjectPath = Path.GetFullPath(
            Path.Combine(sourceDirectory, "SharedKernel", analyzerProjectName, analyzerProjectName + ".csproj"));
        var props = XDocument.Load(Path.Combine(sourceDirectory, "Directory.Build.props"));

        // Act
        var references = props.Descendants("ItemGroup")
            .Where(group => group.Attribute("Condition")?.Value.Contains(OptInCondition, StringComparison.Ordinal) == true)
            .Elements("ProjectReference")
            .Select(reference => reference.Attributes().ToDictionary(
                attribute => attribute.Name.LocalName, attribute => attribute.Value, StringComparer.Ordinal))
            .Where(attributes => attributes.TryGetValue("Include", out var include)
                && include.StartsWith(PropsDirectory, StringComparison.Ordinal)
                && Path.GetFullPath(Path.Combine(sourceDirectory, include[PropsDirectory.Length..])) == analyzerProjectPath)
            .ToArray();
        var analyzerProjectExists = File.Exists(analyzerProjectPath);

        // Assert
        var reference = references.ShouldHaveSingleItem(
            userMessage: "A project that opts in gets exactly one reference to each testing analyzer project.");
        reference.GetValueOrDefault("OutputItemType").ShouldBe(
            "Analyzer", userMessage: "The compiler runs the referenced project as an analyzer.");
        reference.GetValueOrDefault("ReferenceOutputAssembly").ShouldBe(
            "false", userMessage: "Assertion libraries do not compile against the analyzer assembly.");
        reference.GetValueOrDefault("PrivateAssets").ShouldBe(
            "all", userMessage: "The analyzer never becomes a package dependency of an assertion library.");
        reference.GetValueOrDefault("Condition").ShouldBe(
            $"Exists('{reference["Include"]}')",
            userMessage: "The existence guard names the referenced project, so it cannot drop a reference whose path is right.");
        analyzerProjectExists.ShouldBeTrue(
            "The reference names an existing project, so its existence guard does not drop it.");
    }
}
