namespace PandaBear.RepoConfig.Tests;

/// <summary>Creates repositories and configurations for the target framework verification tests.</summary>
internal static class TargetFrameworkTestRepository
{
    /// <summary>The heading that precedes the findings on the standard error.</summary>
    public const string Heading = "Target framework verification failed:";

    /// <summary>The policy of the conforming repository: analyzers, tools with roll-forward, and everything else on two frameworks.</summary>
    public const string Policy = """
        [
          { "include": "src/*.Analyzers/*.csproj", "frameworks": ["netstandard2.0"] },
          { "include": "tools/*.csproj", "frameworks": ["net10.0"], "rollForward": "Major" },
          { "include": "*.csproj", "frameworks": ["net10.0", "net11.0"] }
        ]
        """;

    /// <summary>Returns a configuration whose <c>targetFrameworks</c> section has the given rules, command file patterns, and optional framework properties file.</summary>
    public static string Configuration(string projects = Policy, string commandFiles = """[".github/*", "*.sh", "*.md"]""", string? frameworkProperties = null) =>
        $$"""
        {
          "version": 1,
          "targetFrameworks": {
            "projects": {{projects}},
            "commandFiles": {{commandFiles}}{{(frameworkProperties is null ? string.Empty : $",\n    \"frameworkProperties\": \"{frameworkProperties}\"")}}
          }
        }
        """;

    /// <summary>The file that holds the framework properties of the property-based policies.</summary>
    public const string FrameworksFile = "Directory.Build.props";

    /// <summary>Returns a configuration with one rule for every project that names <paramref name="framework"/> and reads properties from <see cref="FrameworksFile"/>.</summary>
    public static string PropertyPolicy(string framework = "$(Fw)") =>
        Configuration(
            "[{ \"include\": \"*.csproj\", \"frameworks\": [\"" + framework + "\"] }]",
            frameworkProperties: FrameworksFile);

    /// <summary>Returns a project file that sets the given properties in one property group.</summary>
    public static string Project(string properties) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    " + properties + "\n  </PropertyGroup>\n</Project>\n";

    /// <summary>Returns a props or targets file with the given top-level content.</summary>
    public static string Props(string content) => "<Project>\n" + content + "\n</Project>\n";

    /// <summary>Returns a lock file that locks the given target framework keys.</summary>
    public static string Lock(params string[] keys) =>
        "{\"version\":2,\"dependencies\":{" + string.Join(',', keys.Select(static key => "\"" + key + "\":{}")) + "}}";

    /// <summary>Returns the files, keyed by repository-relative path.</summary>
    public static Dictionary<string, string> Files(params (string Path, string Content)[] files) =>
        files.ToDictionary(static file => file.Path, static file => file.Content, StringComparer.Ordinal);

    /// <summary>Creates a Git repository that holds the files and the configuration.</summary>
    public static Task<TemporaryGitRepository> Create(IReadOnlyDictionary<string, string> files, string? configuration = null) =>
        GherkinTestRepository.Create(files, configuration ?? Configuration());

    /// <summary>Runs <c>verify-target-frameworks</c> against the repository.</summary>
    public static Task<TemporaryGitRepository.CommandResult> Run(TemporaryGitRepository repository) =>
        repository.RunCommand(["verify-target-frameworks", "--root", repository.RootPath], TestContext.Current.CancellationToken);

    /// <summary>Returns the standard error lines of a failed run, or the unexpected output of a successful one.</summary>
    public static string[] ErrorLines(TemporaryGitRepository.CommandResult result) => GherkinTestRepository.ErrorLines(result);
}
