using System.Globalization;

namespace SharedKernel.Repositories;

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
          <auditSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </auditSources>
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
            <NuGetAuditMode>all</NuGetAuditMode>
            <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
          </PropertyGroup>
          <PropertyGroup Condition="'$(CI)' == 'true'">
            <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
            <RestoreLockedMode>true</RestoreLockedMode>
          </PropertyGroup>
        </Project>
        """;

    private const string Checks = """
        {
          "lanes": []
        }
        """;

    private const string AllowedSigners = """
        # Add the SSH public keys allowed to sign commits in this repository.
        # Each entry has a principal, optional OpenSSH key constraints, and the public key.
        """;

    private const string PublicationGuardSeed = """
        {
          "version": 1,
          "neverTrack": [".config/publication-guard.local.patterns"],
          "patterns": [],
          "allowlist": []
        }
        """;

    /// <summary>Gets the write-once features in catalog order.</summary>
    internal static IReadOnlyList<RepositoryFeature> Features { get; } =
        [RepositoryFeature.Agents, RepositoryFeature.GlobalJson, RepositoryFeature.NugetConfig, RepositoryFeature.BuildProps,
            RepositoryFeature.CheckRunner, RepositoryFeature.PublicationGuard, RepositoryFeature.SignatureVerifier];

    /// <summary>Renders the files of the selected seed features in catalog order, each with LF line endings and a final newline.</summary>
    /// <param name="repositoryName">The validated repository name.</param>
    /// <param name="features">The selected features; features that are not seeds are ignored.</param>
    /// <param name="sdk">The SDK version; required when a selected feature needs it.</param>
    public static IReadOnlyList<(string Path, string Content)> Render(string repositoryName, IReadOnlyCollection<RepositoryFeature> features, SdkVersion? sdk)
    {
        List<(string, string)> files = [];
        foreach (var feature in Features.Where(features.Contains))
        {
            if (feature == RepositoryFeature.Agents)
            {
                files.Add(("AGENTS.md", Agents.Replace("@@name@@", repositoryName, StringComparison.Ordinal)));
            }
            else if (feature == RepositoryFeature.GlobalJson)
            {
                var version = Required(sdk);
                files.Add(("global.json", GlobalJson
                    .Replace("@@sdk@@", version.Value, StringComparison.Ordinal)
                    .Replace("@@runner@@", version.Major >= 10 ? TestRunner : string.Empty, StringComparison.Ordinal)));
            }
            else if (feature == RepositoryFeature.NugetConfig)
            {
                files.Add(("NuGet.Config", NugetConfig));
            }
            else if (feature == RepositoryFeature.BuildProps)
            {
                files.Add(("Directory.Build.props", BuildProps.Replace("@@major@@", Required(sdk).Major.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)));
            }
            else if (feature == RepositoryFeature.CheckRunner)
            {
                files.Add((TrackedCheckRunner.ConfigurationPath, Checks));
            }
            else if (feature == RepositoryFeature.PublicationGuard)
            {
                files.Add((PublicationGuard.PolicyPath, PublicationGuardSeed));
                files.Add((".config/.gitignore", "/publication-guard.local.patterns"));
            }
            else if (feature == RepositoryFeature.SignatureVerifier)
            {
                files.Add((CommitSignatureVerifier.SignerPath, AllowedSigners));
            }
        }

        return [.. files.Select(static f => (f.Item1, f.Item2.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n"))];
    }

    private static SdkVersion Required(SdkVersion? sdk) =>
        sdk ?? throw new InvalidOperationException("A selected feature needs an SDK version; the caller validates this before rendering.");
}
