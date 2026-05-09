using System.Runtime.InteropServices;

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
        return DotNetCli.RunAsync(ProjectDirectory, "build", ProjectFilePath, "--nologo", "--verbosity", "normal");
    }

    /// <summary>
    /// Runs the consumer project without rebuilding it.
    /// </summary>
    /// <param name="arguments">Additional arguments passed after <c>dotnet run</c>.</param>
    /// <returns>The dotnet run output.</returns>
    public Task<string> Run(params string[] arguments)
    {
        return DotNetCli.RunAsync(ProjectDirectory, ["run", "--project", ProjectFilePath, "--no-build", .. arguments]);
    }

    /// <summary>
    /// Publishes the consumer project.
    /// </summary>
    /// <param name="arguments">Additional arguments passed after <c>dotnet publish</c>.</param>
    /// <returns>The dotnet publish output.</returns>
    public Task<string> Publish(params string[] arguments)
    {
        return DotNetCli.RunAsync(ProjectDirectory, ["publish", ProjectFilePath, .. arguments]);
    }

    /// <summary>
    /// Gets the publish directory for the current consumer project.
    /// </summary>
    /// <param name="configuration">The build configuration. Defaults to <c>Release</c>.</param>
    /// <param name="targetFramework">The target framework. Defaults to <c>net10.0</c>.</param>
    /// <param name="runtimeIdentifier">The runtime identifier when publish output is RID-specific.</param>
    /// <returns>The publish directory path.</returns>
    public string GetPublishDirectory(
        string configuration = "Release",
        string targetFramework = "net10.0",
        string? runtimeIdentifier = null)
    {
        return runtimeIdentifier is null
            ? Path.Combine(ProjectDirectory, "bin", configuration, targetFramework, "publish")
            : Path.Combine(ProjectDirectory, "bin", configuration, targetFramework, runtimeIdentifier, "publish");
    }

    /// <summary>
    /// Gets the published executable path for the consumer project.
    /// </summary>
    /// <param name="runtimeIdentifier">The runtime identifier used for publish.</param>
    /// <param name="configuration">The build configuration. Defaults to <c>Release</c>.</param>
    /// <param name="targetFramework">The target framework. Defaults to <c>net10.0</c>.</param>
    /// <returns>The published executable path.</returns>
    public string GetPublishedExecutablePath(
        string runtimeIdentifier,
        string configuration = "Release",
        string targetFramework = "net10.0")
    {
        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        var projectName = Path.GetFileNameWithoutExtension(ProjectFilePath);
        return Path.Combine(GetPublishDirectory(configuration, targetFramework, runtimeIdentifier), projectName + extension);
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
    /// Writes an additional project into the workspace root alongside the primary project.
    /// </summary>
    /// <param name="projectName">The name of the additional project.</param>
    /// <param name="projectFileContent">The project file content.</param>
    /// <param name="files">The source files to write into the additional project.</param>
    public void WriteAdditionalProject(string projectName, string projectFileContent, params (string FileName, string Content)[] files)
    {
        var projectDirectory = Path.Combine(RootPath, projectName);
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, projectName + ".csproj"), projectFileContent);

        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(projectDirectory, file.FileName), file.Content);
        }
    }

    /// <summary>
    /// Gets a project-reference XML fragment targeting an additional project in this workspace.
    /// </summary>
    /// <param name="projectName">The name of the additional project written via <see cref="WriteAdditionalProject"/>.</param>
    /// <returns>The project reference XML line.</returns>
    public string GetProjectReference(string projectName)
    {
        var projectFilePath = Path.Combine(RootPath, projectName, projectName + ".csproj");
        return $"""<ProjectReference Include="{projectFilePath}" />""";
    }

    /// <summary>
    /// Builds an additional project written via <see cref="WriteAdditionalProject"/>.
    /// </summary>
    /// <param name="projectName">The name of the additional project.</param>
    /// <returns>The dotnet build output.</returns>
    public Task<string> BuildProject(string projectName)
    {
        var projectFilePath = Path.Combine(RootPath, projectName, projectName + ".csproj");
        return DotNetCli.RunAsync(Path.Combine(RootPath, projectName), "build", projectFilePath, "--nologo", "--verbosity", "normal");
    }

    /// <summary>
    /// Gets generated file paths matching <paramref name="fileName"/> inside an additional project directory.
    /// </summary>
    /// <param name="projectName">The name of the additional project.</param>
    /// <param name="fileName">The generated source file name to locate.</param>
    /// <returns>The matching generated file paths.</returns>
    public string[] GetAdditionalProjectGeneratedFiles(string projectName, string fileName)
    {
        var projectDirectory = Path.Combine(RootPath, projectName);
        return Directory.GetFiles(projectDirectory, fileName, SearchOption.AllDirectories);
    }

    /// <summary>
    /// Runs <c>dotnet format</c> targeting the primary project and applies fixes for the specified diagnostic.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic identifier to fix (e.g. <c>SKMED004</c>).</param>
    /// <returns>The dotnet format output.</returns>
    public Task<string> Format(string diagnosticId)
    {
        return DotNetCli.RunAsync(ProjectDirectory, "format", ProjectFilePath, "--diagnostics", diagnosticId);
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
