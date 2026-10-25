using System.Xml.Linq;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

internal sealed class RepositoryTestScope : IDisposable
{
    /// <summary>The configuration that the scope writes: one default slice, <c>library</c>, that lists no project.</summary>
    public const string DefaultConfiguration = """
        {
          "version": 1,
          "solution": "SharedKernel.slnx",
          "selection": {
            "fullValidation": ["*.props", ".github/*"],
            "documentation": ["docs/*", "README.md"],
            "alwaysSelected": [],
            "requireCoveringTests": false
          },
          "slices": [{ "name": "library", "projects": [] }],
          "defaultSlice": "library"
        }
        """;

    public readonly string Root = Path.Combine(Path.GetTempPath(), "sharedkernel-selector-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a solution of a library, a dependent library, and a test project of the dependent, and its CI configuration.</summary>
    public RepositoryTestScope()
    {
        AddProject("src/Core/Core.csproj", """<Project />""");
        AddProject("src/Dependent/Dependent.csproj", """<Project><ProjectReference Include="../Core/Core.csproj" /></Project>""");
        AddProject("tests/Core.Tests/Core.Tests.csproj", """<Project><PackageReference Include="xunit.v3.mtp-v2" /><ProjectReference Include="../../src/Dependent/Dependent.csproj" /></Project>""");
        WriteFile(".config/sharedkernel-ci.json", DefaultConfiguration);
    }

    public void AddProject(string path, string content)
    {
        WriteFile(path, content);
        var solutionPath = Path.Combine(Root, "SharedKernel.slnx");
        var solution = File.Exists(solutionPath) ? XDocument.Load(solutionPath) : new XDocument(new XElement("Solution"));
        solution.Root!.Add(new XElement("Project", new XAttribute("Path", path)));
        solution.Save(solutionPath);
    }

    public void AddTestProject(string path, params string[] references) =>
        AddProject(path, "<Project><PackageReference Include=\"xunit.v3.mtp-v2\" />"
            + string.Concat(references.Select(reference => "<ProjectReference Include=\"" + Path.GetRelativePath(Path.GetDirectoryName(path)!, reference).Replace('\\', '/') + "\" />"))
            + "</Project>");

    public void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Root, path))!);
        File.WriteAllText(Path.Combine(Root, path), content);
    }

    /// <summary>Writes the CI configuration with <paramref name="slices"/> and the other values of <see cref="DefaultConfiguration"/> unless given.</summary>
    public void Configure(
        string slices,
        string? defaultSlice = "library",
        string alwaysSelected = "[]",
        bool requireCoveringTests = false,
        string? testPaths = null) =>
        WriteFile(".config/sharedkernel-ci.json", $$"""
            {
              "version": 1,
              "solution": "SharedKernel.slnx",
              "selection": {
                "fullValidation": ["*.props", ".github/*"],
                "documentation": ["docs/*", "README.md"],
                "alwaysSelected": {{alwaysSelected}},
                "requireCoveringTests": {{(requireCoveringTests ? "true" : "false")}}{{(testPaths is null ? string.Empty : ",\n    \"testPaths\": " + testPaths)}}
              },
              "slices": {{slices}}{{(defaultSlice is null ? string.Empty : ",\n  \"defaultSlice\": \"" + defaultSlice + "\"")}}
            }
            """);

    /// <summary>The test project that <see cref="ConfigureTestPaths"/> maps <c>config/*</c> and <c>assets/*.json</c> to.</summary>
    public const string ConfigTests = "tests/Config.Tests/Config.Tests.csproj";

    /// <summary>Adds <see cref="ConfigTests"/> and a configuration whose <c>testPaths</c> map <c>config/*</c> and <c>assets/*.json</c> to it.</summary>
    public void ConfigureTestPaths(string alwaysSelected = "[]")
    {
        AddTestProject(ConfigTests);
        Configure(
            """[{ "name": "library", "projects": [] }]""",
            alwaysSelected: alwaysSelected,
            testPaths: """[{ "paths": ["config/*", "assets/*.json"], "tests": ["tests/Config.Tests/Config.Tests.csproj"] }]""");
    }

    /// <summary>Writes each of <paramref name="configFiles"/> and a configuration that runs them with Stryker 4.15.0.</summary>
    public void ConfigureMutation(params string[] configFiles)
    {
        foreach (var configFile in configFiles)
        {
            WriteFile(configFile, "{}");
        }

        WriteFile(".config/sharedkernel-ci.json", DefaultConfiguration.Replace(
            "\"version\": 1,",
            "\"version\": 1,\n  \"mutation\": { \"strykerVersion\": \"4.15.0\", \"configFiles\": ["
                + string.Join(", ", configFiles.Select(static file => "\"" + file + "\""))
                + "] },",
            StringComparison.Ordinal));
    }

    /// <summary>Reads the configuration and the solution, and selects for <paramref name="changes"/>.</summary>
    public async Task<SelectionPlan> Select(string[]? changes, string? nonInertBuildFile = null)
    {
        var configuration = await CiConfigurationFile.Read(Root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(Root, configuration.Solution);
        ProjectSelector.Validate(configuration, graph);
        return ProjectSelector.Select(configuration, graph, changes, "fixture", nonInertBuildFile);
    }

    /// <summary>Writes a validation plan whose slices run the given test projects and returns its directory.</summary>
    public string WriteSelection(string revision, params (string Slice, string[] Tests)[] slices) => WriteSelectionIn("TestResults/selection", revision, slices);

    /// <summary>Writes a validation plan in <paramref name="directory"/> under the root and returns its full path.</summary>
    public string WriteSelectionIn(string directory, string revision, params (string Slice, string[] Tests)[] slices)
    {
        // The tool resolves the directory to a full path, which uses the platform separator.
        var selection = Path.GetFullPath(Path.Combine(Root, directory));
        Directory.CreateDirectory(selection);
        var plan = new SelectionPlan(
            revision,
            null,
            false,
            slices.Any(static slice => slice.Tests.Length > 0),
            false,
            [],
            [],
            [.. slices.Select(static slice => new SliceSelection(slice.Slice, slice.Tests.Length > 0, [.. slice.Tests.Select(static test => new SelectedTest(test, []))]))]);
        File.WriteAllBytes(
            Path.Combine(selection, "selection.json"),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(plan, SelectionPlanJsonContext.Default.SelectionPlan));
        return selection;
    }

    public void Dispose()
    {
        // Git writes object files read-only, and Windows refuses to delete read-only files.
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var path in Directory.EnumerateFiles(Root, "*", options))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(Root, recursive: true);
    }
}
