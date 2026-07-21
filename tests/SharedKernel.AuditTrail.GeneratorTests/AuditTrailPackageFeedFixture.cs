namespace SharedKernel.AuditTrail.GeneratorTests;

public sealed class AuditTrailPackageFeedFixture : IAsyncLifetime
{
    private static readonly string[] PackageProjects =
    [
        "src/SharedKernel/SharedKernel.Results/SharedKernel.Results.csproj",
        "src/SharedKernel/SharedKernel.BuildingBlocks/SharedKernel.BuildingBlocks.csproj",
        "src/SharedKernel/SharedKernel.Domain/SharedKernel.Domain.csproj",
        "src/SharedKernel/SharedKernel.Mediator.Abstractions/SharedKernel.Mediator.Abstractions.csproj",
        "src/SharedKernel/SharedKernel.DomainEvents/SharedKernel.DomainEvents.csproj",
        "src/SharedKernel/SharedKernel.AuditTrail/SharedKernel.AuditTrail.csproj",
        "src/SharedKernel/SharedKernel.AuditTrail.SourceGenerator/SharedKernel.AuditTrail.SourceGenerator.csproj"
    ];

    public string PackageVersion { get; } = $"1.0.0-test-{Guid.NewGuid():N}";

    public string FeedPath { get; private set; } = null!;

    private string RootPath { get; set; } = null!;

    private string RepositoryRoot { get; set; } = null!;

    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        RootPath = Path.Combine(Path.GetTempPath(), $"sharedkernel-audit-trail-package-feed-{Guid.NewGuid():N}");
        FeedPath = Path.Combine(RootPath, "feed");
        Directory.CreateDirectory(FeedPath);

        foreach (var relativeProjectPath in PackageProjects)
        {
            var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);
            _ = await AuditTrailPackageConsumptionWorkspace.RunDotNet(
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
            if (File.Exists(Path.Combine(currentDirectory.FullName, "ViajantesTurismo.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
