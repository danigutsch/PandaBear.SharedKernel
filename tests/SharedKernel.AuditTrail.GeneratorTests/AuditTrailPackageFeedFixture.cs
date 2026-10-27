using PandaBear.Testing.Packaging;

namespace PandaBear.AuditTrail.GeneratorTests;

/// <summary>
/// Packs the audit trail packages and their dependencies into a temporary local NuGet feed for package consumption tests.
/// </summary>
public sealed class AuditTrailPackageFeedFixture : IAsyncLifetime
{
    private static readonly string[] PackageProjects =
    [
        "src/SharedKernel/SharedKernel.Results/SharedKernel.Results.csproj",
        "src/SharedKernel/SharedKernel.BuildingBlocks/SharedKernel.BuildingBlocks.csproj",
        "src/SharedKernel/SharedKernel.Domain/SharedKernel.Domain.csproj",
        "src/SharedKernel/SharedKernel.AuditTrail/SharedKernel.AuditTrail.csproj",
        "src/SharedKernel/SharedKernel.AuditTrail.SourceGenerator/SharedKernel.AuditTrail.SourceGenerator.csproj"
    ];

    /// <summary>
    /// Unique prerelease version assigned to every package packed into the feed.
    /// </summary>
    public string PackageVersion { get; } = $"1.0.0-test-{Guid.NewGuid():N}";

    /// <summary>
    /// Directory holding the packed packages after initialization.
    /// </summary>
    public string FeedPath { get; private set; } = null!;

    private string RootPath { get; set; } = null!;

    /// <summary>
    /// Repository root directory, located by searching upward from the test output for SharedKernel.slnx.
    /// </summary>
    internal string RepositoryRoot { get; private set; } = null!;

    /// <summary>
    /// Creates a consumption workspace for the named project that restores packages from this feed at the packed version.
    /// </summary>
    internal PackageConsumptionWorkspace CreateWorkspace(string projectName) =>
        new(RepositoryRoot, FeedPath, PackageVersion, projectName);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        RootPath = Path.Combine(Path.GetTempPath(), $"sharedkernel-audit-trail-package-feed-{Guid.NewGuid():N}");
        FeedPath = Path.Combine(RootPath, "feed");
        Directory.CreateDirectory(FeedPath);

        foreach (var relativeProjectPath in PackageProjects)
        {
            var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);
            _ = await DotNetCli.Run(
                RepositoryRoot,
                "pack",
                projectPath,
                "-c",
                "Release",
                $"-p:PackageVersion={PackageVersion}",
                "-o",
                FeedPath,
                "--nologo");
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(RootPath) && Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "SharedKernel.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
