using System.Xml.Linq;

namespace SharedKernel.Mediator.PackageConsumptionTests;

/// <summary>
/// Packs the current mediator packages into a temporary local NuGet feed for consumption tests.
/// </summary>
public sealed class MediatorPackageFeedFixture : IAsyncLifetime
{
    private const string TestPackageVersion = "1.0.0-package-test";

    /// <summary>
    /// Gets the packed package version used by the test feed.
    /// </summary>
    public string PackageVersion { get; } = TestPackageVersion;

    /// <summary>
    /// Gets the local feed path that contains the packed mediator packages.
    /// </summary>
    public string FeedPath { get; private set; } = null!;

    /// <summary>
    /// Gets the Microsoft.Extensions.DependencyInjection package version pinned by the repository.
    /// </summary>
    public string DependencyInjectionPackageVersion { get; private set; } = null!;

    private string RootPath { get; set; } = null!;

    private string RepositoryRoot { get; set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        DependencyInjectionPackageVersion = ReadCentralPackageVersion("Microsoft.Extensions.DependencyInjection");

        RootPath = Path.Combine(Path.GetTempPath(), $"sharedkernel-mediator-package-feed-{Guid.NewGuid():N}");
        FeedPath = Path.Combine(RootPath, "feed");
        Directory.CreateDirectory(FeedPath);

        await Pack("src/Mediator/SharedKernel.Mediator.Abstractions/SharedKernel.Mediator.Abstractions.csproj").ConfigureAwait(false);
        await Pack("src/Mediator/SharedKernel.Mediator/SharedKernel.Mediator.csproj").ConfigureAwait(false);
        await Pack("src/Mediator/SharedKernel.Mediator.SourceGenerator/SharedKernel.Mediator.SourceGenerator.csproj").ConfigureAwait(false);
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

    private async Task Pack(string relativeProjectPath)
    {
        var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);
        await DotNetCli.RunAsync(
                RepositoryRoot,
                "pack",
                projectPath,
                "-c",
                "Release",
                "-p:PackageVersion=" + PackageVersion,
                "-o",
                FeedPath,
                "--nologo")
            .ConfigureAwait(false);
    }

    private string ReadCentralPackageVersion(string packageId)
    {
        var packagesFilePath = Path.Combine(RepositoryRoot, "Directory.Packages.props");
        var document = XDocument.Load(packagesFilePath);
        var packageVersion = document
            .Descendants("PackageVersion")
            .SingleOrDefault(element => string.Equals((string?)element.Attribute("Include"), packageId, StringComparison.Ordinal))
            ?.Attribute("Version")
            ?.Value;

        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            throw new InvalidOperationException($"Could not find package version for '{packageId}' in Directory.Packages.props.");
        }

        return packageVersion;
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "ViajantesTurismo.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
