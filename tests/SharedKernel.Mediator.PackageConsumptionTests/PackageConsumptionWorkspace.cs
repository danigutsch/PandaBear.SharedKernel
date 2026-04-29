namespace SharedKernel.Mediator.PackageConsumptionTests;

/// <summary>
/// Creates isolated consumer projects that restore packages from the temporary mediator feed.
/// </summary>
internal sealed class PackageConsumptionWorkspace : IDisposable
{
    /// <summary>
    /// Initializes a new temporary package-consumption workspace.
    /// </summary>
    /// <param name="packageFeed">The packed mediator package feed fixture.</param>
    /// <param name="projectName">The project name used inside the workspace.</param>
    public PackageConsumptionWorkspace(MediatorPackageFeedFixture packageFeed, string projectName)
    {
        PackageFeed = packageFeed;
        RootPath = Path.Combine(Path.GetTempPath(), $"{projectName}-{Guid.NewGuid():N}");
        ProjectDirectory = Path.Combine(RootPath, projectName);
        ProjectFilePath = Path.Combine(ProjectDirectory, projectName + ".csproj");

        Directory.CreateDirectory(ProjectDirectory);
        File.WriteAllText(Path.Combine(RootPath, "NuGet.Config"), CreateNuGetConfig(packageFeed.FeedPath));
    }

    private MediatorPackageFeedFixture PackageFeed { get; }

    private string RootPath { get; }

    private string ProjectDirectory { get; }

    private string ProjectFilePath { get; }

    /// <summary>
    /// Writes the project file and source files for the consumer workspace.
    /// </summary>
    /// <param name="projectFileContent">The project file content.</param>
    /// <param name="files">The source files to write into the consumer project.</param>
    public void WriteProject(string projectFileContent, params (string FileName, string Content)[] files)
    {
        File.WriteAllText(ProjectFilePath, projectFileContent);

        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(ProjectDirectory, file.FileName), file.Content);
        }
    }

    /// <summary>
    /// Builds the consumer project.
    /// </summary>
    /// <returns>The dotnet build output.</returns>
    public Task<string> Build()
    {
        return DotNetCli.RunAsync(ProjectDirectory, "build", ProjectFilePath, "--nologo");
    }

    /// <summary>
    /// Gets the generated file paths for a given generated source file name.
    /// </summary>
    /// <param name="fileName">The generated source file name to locate.</param>
    /// <returns>The matching generated file paths.</returns>
    public string[] GetGeneratedFiles(string fileName)
    {
        return Directory.GetFiles(ProjectDirectory, fileName, SearchOption.AllDirectories);
    }

    /// <summary>
    /// Gets a package-reference XML fragment for the shared mediator packages.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="additionalAttributes">Optional extra attributes to append to the package reference.</param>
    /// <returns>The package reference XML line.</returns>
    public string GetPackageReference(string packageId, string? additionalAttributes = null)
    {
        var attributes = string.IsNullOrWhiteSpace(additionalAttributes)
            ? string.Empty
            : " " + additionalAttributes.Trim();

        return $"""<PackageReference Include="{packageId}" Version="{PackageFeed.PackageVersion}"{attributes} />""";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static string CreateNuGetConfig(string feedPath)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{feedPath}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """;
    }
}
