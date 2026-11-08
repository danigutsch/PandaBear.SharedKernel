using System.Globalization;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The neutral files that <c>new</c> writes once and never manages afterwards: formats without a comment for the
/// managed-file marker, and files the owner is expected to edit. They name no owner, forge, or registry other than nuget.org.
/// </summary>
internal static class NewRepositorySeeds
{
    private const string Agents = """
        # Agent instructions

        Instructions for coding agents working in @@name@@.

        ## Principles

        - Make the smallest change that fully solves the task and fix the root cause.
        - Read the code and tests around a change before editing.
        - Never commit secrets or personal data.

        ## Workflow

        - Work in a Git worktree, not in the main checkout.
        - Run the checks before calling a change done, and report what actually ran.
        - Use Conventional Commits.

        ## Where to look

        - `README.md` for how to build, test, and run.
        """;

    private const string GlobalJson = """
        {
          "sdk": {
            "version": "@@sdk@@",
            "rollForward": "disable",
            "errorMessage": "Install .NET SDK @@sdk@@, the version that global.json pins."
          }@@runner@@
        }
        """;

    private const string TestRunner = """
        ,
          "test": {
            "runner": "Microsoft.Testing.Platform"
          }
        """;

    private const string NugetConfig = """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

    private const string BuildProps = """
        <Project>
          <PropertyGroup>
            <TargetFramework>net@@major@@.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <AnalysisMode>All</AnalysisMode>
            <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
            <NuGetAudit>true</NuGetAudit>
            <NuGetAuditLevel>high</NuGetAuditLevel>
            <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
          </PropertyGroup>
          <PropertyGroup Condition="'$(CI)' == 'true'">
            <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
            <RestoreLockedMode>true</RestoreLockedMode>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>Renders the files of the selected seed features in catalog order, each with LF line endings and a final newline.</summary>
    /// <param name="repositoryName">The validated repository name.</param>
    /// <param name="features">The selected features; features that are not seeds are ignored.</param>
    /// <param name="sdk">The SDK version; required when a selected feature needs it.</param>
    public static IReadOnlyList<(string Path, string Content)> Render(string repositoryName, IReadOnlyCollection<RepoFeature> features, SdkVersion? sdk)
    {
        List<(string, string)> files = [];
        foreach (var feature in RepoFeature.All.Where(features.Contains))
        {
            if (feature == RepoFeature.Agents)
            {
                files.Add(("AGENTS.md", Agents.Replace("@@name@@", repositoryName, StringComparison.Ordinal)));
            }
            else if (feature == RepoFeature.GlobalJson)
            {
                var version = Required(sdk);
                files.Add(("global.json", GlobalJson
                    .Replace("@@sdk@@", version.Value, StringComparison.Ordinal)
                    .Replace("@@runner@@", version.Major >= 10 ? TestRunner : string.Empty, StringComparison.Ordinal)));
            }
            else if (feature == RepoFeature.NugetConfig)
            {
                files.Add(("NuGet.Config", NugetConfig));
            }
            else if (feature == RepoFeature.BuildProps)
            {
                files.Add(("Directory.Build.props", BuildProps.Replace("@@major@@", Required(sdk).Major.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)));
            }
        }

        return [.. files.Select(static f => (f.Item1, f.Item2.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n"))];
    }

    private static SdkVersion Required(SdkVersion? sdk) =>
        sdk ?? throw new InvalidOperationException("A selected feature needs an SDK version; the caller validates this before rendering.");
}
