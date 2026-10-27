using System.Xml.Linq;

namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Disposable temporary directory that builds a fake repository of .csproj files, a solution, and CI test slice manifests for CI test selection tests.
/// </summary>
internal sealed class TemporaryCiTestRepository : IDisposable
{
    private readonly List<string> _projectPaths = [];
    private readonly TemporaryDirectory _directory = new();

    /// <summary>
    /// Creates the temporary repository directory and records its root path.
    /// </summary>
    public TemporaryCiTestRepository()
    {
        RootPath = _directory.Path;
    }

    /// <summary>
    /// Gets the absolute path of the temporary repository root.
    /// </summary>
    public string RootPath
    {
        get;
    }

    /// <summary>
    /// Writes a plain project at the relative path with the given project references and registers it in the solution.
    /// </summary>
    public void AddProject(string relativePath, params string[] projectReferences)
    {
        AddProject(relativePath, useWindowsReferenceSeparators: false, isXunitTestProject: false, projectReferences);
    }

    /// <summary>
    /// Writes a project that references the xunit.v3.mtp-v2 package so it counts as a test project, with the given project references.
    /// </summary>
    public void AddXunitProject(string relativePath, params string[] projectReferences)
    {
        AddProject(relativePath, useWindowsReferenceSeparators: false, isXunitTestProject: true, projectReferences);
    }

    /// <summary>
    /// Writes a plain project whose project references use backslash separators, as authored on Windows.
    /// </summary>
    public void AddProjectWithWindowsReferences(string relativePath, params string[] projectReferences)
    {
        AddProject(relativePath, useWindowsReferenceSeparators: true, isXunitTestProject: false, projectReferences);
    }

    private void AddProject(
        string relativePath,
        bool useWindowsReferenceSeparators,
        bool isXunitTestProject,
        params string[] projectReferences)
    {
        var normalizedPath = Normalize(relativePath);
        var fullPath = Path.Combine(RootPath, normalizedPath);
        var projectDirectory = Path.GetDirectoryName(fullPath);
        projectDirectory.ShouldNotBeNull();
        Directory.CreateDirectory(projectDirectory);

        var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));
        if (isXunitTestProject)
        {
            project.Add(new XElement(
                "ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "xunit.v3.mtp-v2"))));
        }

        if (projectReferences.Length > 0)
        {
            project.Add(new XElement(
                "ItemGroup",
                projectReferences.Select(reference => new XElement(
                    "ProjectReference",
                    new XAttribute(
                        "Include",
                        useWindowsReferenceSeparators
                            ? Path.GetRelativePath(projectDirectory, Path.Combine(RootPath, Normalize(reference))).Replace('/', '\\')
                            : Path.GetRelativePath(projectDirectory, Path.Combine(RootPath, Normalize(reference))))))));
        }

        new XDocument(project).Save(fullPath);
        _projectPaths.Add(normalizedPath);
        WriteSolution();
    }

    /// <summary>
    /// Writes the slice manifest scripts/ci-test-slices/{name}.txt listing the given project paths.
    /// </summary>
    public void WriteSlice(string name, params string[] projectPaths)
    {
        var manifestsDirectory = Path.Combine(RootPath, "scripts", "ci-test-slices");
        Directory.CreateDirectory(manifestsDirectory);
        File.WriteAllLines(
            Path.Combine(manifestsDirectory, $"{name}.txt"),
            projectPaths.Select(Normalize));
    }

    /// <summary>
    /// Adds the six canonical xunit test projects and one slice manifest for each of them.
    /// </summary>
    public void AddCanonicalTestSlices()
    {
        AddXunitProject("tests/AdminApi.Tests/AdminApi.Tests.csproj");
        AddXunitProject("tests/AdminIntegration.Tests/AdminIntegration.Tests.csproj");
        AddXunitProject("tests/AdminSystem.Tests/AdminSystem.Tests.csproj");
        AddXunitProject("tests/Fast1.Tests/Fast1.Tests.csproj");
        AddXunitProject("tests/Fast2.Tests/Fast2.Tests.csproj");
        AddXunitProject("tests/Mediator.Tests/Mediator.Tests.csproj");
        WriteSlice("admin-api-integration", "tests/AdminApi.Tests/AdminApi.Tests.csproj");
        WriteSlice("admin-integration", "tests/AdminIntegration.Tests/AdminIntegration.Tests.csproj");
        WriteSlice("admin-system", "tests/AdminSystem.Tests/AdminSystem.Tests.csproj");
        WriteSlice("fast-validation-1", "tests/Fast1.Tests/Fast1.Tests.csproj");
        WriteSlice("fast-validation-2", "tests/Fast2.Tests/Fast2.Tests.csproj");
        WriteSlice("mediator-heavy", "tests/Mediator.Tests/Mediator.Tests.csproj");
    }

    /// <summary>
    /// Deletes the slice manifest with the given name.
    /// </summary>
    public void DeleteSlice(string name)
    {
        File.Delete(Path.Combine(RootPath, "scripts", "ci-test-slices", $"{name}.txt"));
    }

    /// <summary>
    /// Writes a file with the given content under the repository root, creating missing directories.
    /// </summary>
    public void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, Normalize(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? RootPath);
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// Moves a file between two repository-relative paths, creating the destination directory.
    /// </summary>
    public void MoveFile(string sourcePath, string destinationPath)
    {
        var destination = Path.Combine(RootPath, Normalize(destinationPath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? RootPath);
        File.Move(Path.Combine(RootPath, Normalize(sourcePath)), destination);
    }

    /// <summary>
    /// Deletes the temporary repository directory.
    /// </summary>
    public void Dispose()
    {
        _directory.Dispose();
    }

    private void WriteSolution()
    {
        var solution = new XDocument(
            new XElement(
                "Solution",
                _projectPaths.Select(path => new XElement("Project", new XAttribute("Path", path)))));
        solution.Save(Path.Combine(RootPath, "SharedKernel.slnx"));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
