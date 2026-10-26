using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

public static class SharedKernelPackagingConventionTests
{
    [Fact]
    public static void SharedKernel_source_projects_follow_package_metadata_conventions()
    {
        // Arrange
        var repositoryRoot = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var projectFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "src", "SharedKernel"),
            "*.csproj",
            SearchOption.AllDirectories);

        // Act
        var violations = projectFiles
            .Where(project => !string.Equals(
                SharedKernelPackagingConventionTestFiles.GetProperty(project, "IsPackable"),
                "false",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(SharedKernelPackagingConventionTestFiles.GetPackageMetadataViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        violations.ShouldBe([]);
    }

    [Fact]
    public static void SharedKernel_roslyn_packages_use_analyzer_only_layout()
    {
        // Arrange
        var repositoryRoot = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        (string ProjectPath, string[] AnalyzerDllNames)[] packages =
        [
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Testing.Analyzers", "SharedKernel.Testing.Analyzers.csproj"),
                ["SharedKernel.Testing.Analyzers.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Testing.CodeFixes", "SharedKernel.Testing.CodeFixes.csproj"),
                ["SharedKernel.Testing.Analyzers.dll", "SharedKernel.Testing.CodeFixes.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Style.Analyzers", "SharedKernel.Style.Analyzers.csproj"),
                ["SharedKernel.Style.Analyzers.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Style.CodeFixes", "SharedKernel.Style.CodeFixes.csproj"),
                ["SharedKernel.Style.Analyzers.dll", "SharedKernel.Style.CodeFixes.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Domain.Analyzers", "SharedKernel.Domain.Analyzers.csproj"),
                ["SharedKernel.Domain.Analyzers.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Domain.CodeFixes", "SharedKernel.Domain.CodeFixes.csproj"),
                ["SharedKernel.Domain.CodeFixes.dll"]),
        ];

        // Act
        var violations = packages
            .SelectMany(package => SharedKernelPackagingConventionTestFiles.GetAnalyzerPackageLayoutViolations(package.ProjectPath, package.AnalyzerDllNames))
            .ToArray();

        // Assert
        violations.ShouldBe([]);
    }

    [Fact]
    public static void Shared_build_files_supply_license_repository_source_link_and_symbol_metadata()
    {
        // Arrange
        var repositoryRoot = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var props = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var targets = XDocument.Load(Path.Combine(repositoryRoot, "src", "Directory.Build.targets"));

        // Act
        var license = SharedKernelPackagingConventionTestFiles.GetProperty(props, "PackageLicenseExpression");
        var repositoryUrl = SharedKernelPackagingConventionTestFiles.GetProperty(props, "RepositoryUrl");
        var publishRepositoryUrl = SharedKernelPackagingConventionTestFiles.GetProperty(props, "PublishRepositoryUrl");
        var includeSymbols = SharedKernelPackagingConventionTestFiles.GetProperty(targets, "IncludeSymbols");
        var symbolFormat = SharedKernelPackagingConventionTestFiles.GetProperty(targets, "SymbolPackageFormat");

        // Assert
        license.ShouldBe("MIT");
        repositoryUrl.ShouldBe("https://github.com/danigutsch/PandaBear.SharedKernel");
        publishRepositoryUrl.ShouldBe("true");
        includeSymbols.ShouldBe("true");
        symbolFormat.ShouldBe("snupkg");
    }

    [Fact]
    public static void Source_generators_mark_generated_code_as_auto_generated_with_nullable_enabled()
    {
        // Arrange
        var generatorDirectories = Directory.GetDirectories(
            Path.Combine(SharedKernelPackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel"),
            "SharedKernel.*.SourceGenerator");

        // Act
        var violations = generatorDirectories
            .SelectMany(directory =>
            {
                var sources = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    .Select(File.ReadAllText)
                    .ToArray();
                return new[]
                    {
                        (Rule: "an `// <auto-generated />` header", Present: sources.Any(text => text.Contains("// <auto-generated />", StringComparison.Ordinal))),
                        (Rule: "`#nullable enable` directly after the header", Present: sources.Any(text => Regex.IsMatch(text, "// <auto-generated />[^#]{0,40}#nullable enable", RegexOptions.None, TimeSpan.FromSeconds(5)))),
                    }
                    .Where(check => !check.Present)
                    .Select(check => $"{Path.GetFileName(directory)} emits no generated source with {check.Rule}.");
            })
            .ToArray();

        // Assert
        generatorDirectories.ShouldNotBeEmpty();
        violations.ShouldBe([]);
    }

    [Fact]
    public static void Source_generators_use_hint_names_that_end_in_g_cs()
    {
        // Arrange
        var generatorDirectories = Directory.GetDirectories(
            Path.Combine(SharedKernelPackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel"),
            "SharedKernel.*.SourceGenerator");

        // Act
        var violations = generatorDirectories
            .SelectMany(directory => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .SelectMany(path => SharedKernelPackagingConventionTestFiles.GetHintNameLiteralViolations(path, File.ReadAllText(path))))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        generatorDirectories.ShouldNotBeEmpty();
        violations.ShouldBe([]);
    }

    [Fact]
    public static void Ci_tool_is_packaged_as_the_sharedkernel_ci_dotnet_tool()
    {
        // Arrange
        var projectPath = Path.Combine(
            SharedKernelPackagingConventionTestFiles.GetRepositoryRoot(),
            "tools",
            "SharedKernel.CI.Tool",
            "SharedKernel.CI.Tool.csproj");

        // Act
        var violations = SharedKernelPackagingConventionTestFiles.GetPackageMetadataViolations(projectPath).ToArray();
        var isPackable = SharedKernelPackagingConventionTestFiles.GetProperty(projectPath, "IsPackable");
        var packAsTool = SharedKernelPackagingConventionTestFiles.GetProperty(projectPath, "PackAsTool");
        var commandName = SharedKernelPackagingConventionTestFiles.GetProperty(projectPath, "ToolCommandName");
        var description = SharedKernelPackagingConventionTestFiles.GetProperty(projectPath, "Description");
        var aotCompatible = SharedKernelPackagingConventionTestFiles.GetProperty(projectPath, "IsAotCompatible");

        // Assert
        violations.ShouldBe([]);
        isPackable.ShouldBe("true");
        packAsTool.ShouldBe("true");
        commandName.ShouldBe("sharedkernel-ci", "Tool command names start with sharedkernel- and name the tool.");
        description.ShouldNotBe(string.Empty);
        aotCompatible.ShouldBe("true");
    }

    [Fact]
    public static void Samples_benchmarks_and_repository_only_helpers_are_non_packable()
    {
        // Arrange
        var repositoryRoot = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        string[] projectPaths =
        [
            "benchmarks/SharedKernel.Functional.Benchmarks/SharedKernel.Functional.Benchmarks.csproj",
            "benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj",
            "samples/Mediator/Mediator.Sample/Mediator.Sample.csproj",
            "samples/Results/BasicResults.Sample/BasicResults.Sample.csproj",
            "tests/SharedKernel.CodeFixes.Testing/SharedKernel.CodeFixes.Testing.csproj",
            "tests/SharedKernel.Testing.Integration/SharedKernel.Testing.Integration.csproj",
            "tests/SharedKernel.Testing.Packaging/SharedKernel.Testing.Packaging.csproj",
            "tests/SharedKernel.Testing.Scenarios/SharedKernel.Testing.Scenarios.csproj",
            "tests/SharedKernel.Testing.System/SharedKernel.Testing.System.csproj",
        ];

        // Act
        var packableProjects = projectPaths
            .Where(path => !string.Equals(
                SharedKernelPackagingConventionTestFiles.GetProperty(Path.Combine(repositoryRoot, path), "IsPackable"),
                "false",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        packableProjects.ShouldBe([]);
    }
}
