using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>Guards the package metadata, analyzer layout, source generator, and non-packable project conventions of the repository.</summary>
public static class PackagingConventionTests
{
    [Fact]
    public static void SharedKernel_source_projects_follow_package_metadata_conventions()
    {
        // Arrange
        var repositoryRoot = PackagingConventionTestFiles.GetRepositoryRoot();
        var projectFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "src", "SharedKernel"),
            "*.csproj",
            SearchOption.AllDirectories);

        // Act
        var violations = projectFiles
            .Where(project => !string.Equals(
                PackagingConventionTestFiles.GetProperty(project, "IsPackable"),
                "false",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(PackagingConventionTestFiles.GetPackageMetadataViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        violations.ShouldBe([]);
    }

    [Fact]
    public static void SharedKernel_roslyn_packages_use_analyzer_only_layout()
    {
        // Arrange
        var repositoryRoot = PackagingConventionTestFiles.GetRepositoryRoot();
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
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Blazor.Analyzers", "SharedKernel.Blazor.Analyzers.csproj"),
                ["SharedKernel.Blazor.Analyzers.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Blazor.CodeFixes", "SharedKernel.Blazor.CodeFixes.csproj"),
                ["SharedKernel.Blazor.CodeFixes.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Results.Analyzers", "SharedKernel.Results.Analyzers.csproj"),
                ["SharedKernel.Results.Analyzers.dll"]),
            (
                Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.EntityFrameworkCore.Analyzers", "SharedKernel.EntityFrameworkCore.Analyzers.csproj"),
                ["SharedKernel.EntityFrameworkCore.Analyzers.dll"]),
        ];

        // Act
        var violations = packages
            .SelectMany(package => PackagingConventionTestFiles.GetAnalyzerPackageLayoutViolations(package.ProjectPath, package.AnalyzerDllNames))
            .ToArray();

        // Assert
        violations.ShouldBe([]);
    }

    [Fact]
    public static void Shared_build_files_supply_license_repository_source_link_and_symbol_metadata()
    {
        // Arrange
        var repositoryRoot = PackagingConventionTestFiles.GetRepositoryRoot();
        var props = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var targets = XDocument.Load(Path.Combine(repositoryRoot, "src", "Directory.Build.targets"));

        // Act
        var license = PackagingConventionTestFiles.GetProperty(props, "PackageLicenseExpression");
        var repositoryUrl = PackagingConventionTestFiles.GetProperty(props, "RepositoryUrl");
        var publishRepositoryUrl = PackagingConventionTestFiles.GetProperty(props, "PublishRepositoryUrl");
        var includeSymbols = PackagingConventionTestFiles.GetProperty(targets, "IncludeSymbols");
        var symbolFormat = PackagingConventionTestFiles.GetProperty(targets, "SymbolPackageFormat");

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
            Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel"),
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
            Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel"),
            "SharedKernel.*.SourceGenerator");

        // Act
        var violations = generatorDirectories
            .SelectMany(directory => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .SelectMany(path => PackagingConventionTestFiles.GetHintNameLiteralViolations(path, File.ReadAllText(path))))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        generatorDirectories.ShouldNotBeEmpty();
        violations.ShouldBe([]);
    }

    [Fact]
    public static void Ci_tool_is_packaged_as_the_ci_dotnet_tool()
    {
        // Arrange
        var projectPath = Path.Combine(
            PackagingConventionTestFiles.GetRepositoryRoot(),
            "tools",
            "SharedKernel.CI.Tool",
            "SharedKernel.CI.Tool.csproj");

        // Act
        var violations = PackagingConventionTestFiles.GetPackageMetadataViolations(projectPath).ToArray();
        var isPackable = PackagingConventionTestFiles.GetProperty(projectPath, "IsPackable");
        var packAsTool = PackagingConventionTestFiles.GetProperty(projectPath, "PackAsTool");
        var commandName = PackagingConventionTestFiles.GetProperty(projectPath, "ToolCommandName");
        var description = PackagingConventionTestFiles.GetProperty(projectPath, "Description");
        var aotCompatible = PackagingConventionTestFiles.GetProperty(projectPath, "IsAotCompatible");

        // Assert
        violations.ShouldBe([]);
        isPackable.ShouldBe("true");
        packAsTool.ShouldBe("true");
        commandName.ShouldBe("ci", "Tool command names name the tool without a repository prefix.");
        description.ShouldNotBe(string.Empty);
        aotCompatible.ShouldBe("true");
    }

    [Fact]
    public static void Tool_command_names_are_unique_unprefixed_and_not_dotnet_sdk_commands()
    {
        // Arrange
        var toolsDirectory = Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), "tools");
        string[] sdkCommands =
        [
            "add", "build", "build-server", "clean", "dev-certs", "dnx", "exec", "format", "fsi", "help", "list", "msbuild", "new",
            "nuget", "pack", "package", "project", "publish", "reference", "remove", "restore", "run", "sdk", "sln", "solution",
            "store", "test", "tool", "user-jwts", "user-secrets", "vstest", "watch", "workload",
        ];

        // Act
        var commandNames = Directory.GetFiles(toolsDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(static projectPath => PackagingConventionTestFiles.GetProperty(projectPath, "PackAsTool") == "true")
            .Select(static projectPath => PackagingConventionTestFiles.GetProperty(projectPath, "ToolCommandName"))
            .ToArray();
        var violations = commandNames
            .Where(name => name.Length == 0
                || name.Contains("sharedkernel", StringComparison.OrdinalIgnoreCase)
                || name.Contains("pandabear", StringComparison.OrdinalIgnoreCase)
                || sdkCommands.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        commandNames.ShouldNotBeEmpty();
        violations.ShouldBe([]);
        commandNames.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(commandNames.Length, "Two tools cannot share a command.");
    }

    [Fact]
    public static void Samples_benchmarks_and_repository_only_helpers_are_non_packable()
    {
        // Arrange
        var repositoryRoot = PackagingConventionTestFiles.GetRepositoryRoot();
        string[] projectPaths =
        [
            "benchmarks/SharedKernel.Results.Benchmarks/SharedKernel.Results.Benchmarks.csproj",
            "benchmarks/SharedKernel.Mediator.Benchmarks/SharedKernel.Mediator.Benchmarks.csproj",
            "samples/Mediator/Mediator.Sample/Mediator.Sample.csproj",
            "samples/Results/BasicResults.Sample/BasicResults.Sample.csproj",
            "tests/SharedKernel.Testing.Integration/SharedKernel.Testing.Integration.csproj",
            "tests/SharedKernel.Testing.Packaging/SharedKernel.Testing.Packaging.csproj",
            "tests/SharedKernel.Testing.Scenarios/SharedKernel.Testing.Scenarios.csproj",
            "tests/SharedKernel.Testing.System/SharedKernel.Testing.System.csproj",
        ];

        // Act
        var packableProjects = projectPaths
            .Where(path => !string.Equals(
                PackagingConventionTestFiles.GetProperty(Path.Combine(repositoryRoot, path), "IsPackable"),
                "false",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        packableProjects.ShouldBe([]);
    }
}
