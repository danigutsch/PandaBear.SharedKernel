using System.Xml.Linq;

namespace SharedKernel.RepoConfig.Tests;

internal sealed class TemporaryCiTestRepository : IDisposable
{
    private readonly List<string> _projectPaths = [];

    public TemporaryCiTestRepository()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"sharedkernel-ci-selection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public void AddProject(string relativePath, params string[] projectReferences)
    {
        AddProject(relativePath, useWindowsReferenceSeparators: false, isXunitTestProject: false, projectReferences);
    }

    public void AddXunitProject(string relativePath, params string[] projectReferences)
    {
        AddProject(relativePath, useWindowsReferenceSeparators: false, isXunitTestProject: true, projectReferences);
    }

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

    public void WriteSlice(string name, params string[] projectPaths)
    {
        var manifestsDirectory = Path.Combine(RootPath, "scripts", "ci-test-slices");
        Directory.CreateDirectory(manifestsDirectory);
        File.WriteAllLines(
            Path.Combine(manifestsDirectory, $"{name}.txt"),
            projectPaths.Select(Normalize));
    }

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

    public void DeleteSlice(string name)
    {
        File.Delete(Path.Combine(RootPath, "scripts", "ci-test-slices", $"{name}.txt"));
    }

    public void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, Normalize(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? RootPath);
        File.WriteAllText(fullPath, content);
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        var destination = Path.Combine(RootPath, Normalize(destinationPath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? RootPath);
        File.Move(Path.Combine(RootPath, Normalize(sourcePath)), destination);
    }

    public void Dispose()
    {
        TemporaryRepositoryDirectory.Delete(RootPath);
    }

    private void WriteSolution()
    {
        var solution = new XDocument(
            new XElement(
                "Solution",
                _projectPaths.Select(path => new XElement("Project", new XAttribute("Path", path)))));
        solution.Save(Path.Combine(RootPath, "ViajantesTurismo.slnx"));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
