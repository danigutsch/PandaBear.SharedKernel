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
        ];

        // Act
        var violations = packages
            .SelectMany(package => SharedKernelPackagingConventionTestFiles.GetAnalyzerPackageLayoutViolations(package.ProjectPath, package.AnalyzerDllNames))
            .ToArray();

        // Assert
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
