using System.Xml.Linq;

namespace PandaBear.Repository.Tests;

internal sealed class RepositoryTestScope : IDisposable
{
    public readonly string Root = Path.Combine(Path.GetTempPath(), "sharedkernel-selector-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a solution independent of extraction provenance.</summary>
    public RepositoryTestScope()
    {
        AddProject("src/Core/Core.csproj", """<Project />""");
        AddProject("src/Dependent/Dependent.csproj", """<Project><ProjectReference Include="../Core/Core.csproj" /></Project>""");
        AddProject("tests/Core.Tests/Core.Tests.csproj", """<Project><PackageReference Include="xunit.v3.mtp-v2" /><ProjectReference Include="../../src/Dependent/Dependent.csproj" /></Project>""");
    }

    public void AddProject(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Root, path))!);
        File.WriteAllText(Path.Combine(Root, path), content);
        var solutionPath = Path.Combine(Root, "SharedKernel.slnx");
        var solution = File.Exists(solutionPath) ? XDocument.Load(solutionPath) : new XDocument(new XElement("Solution"));
        solution.Root!.Add(new XElement("Project", new XAttribute("Path", path)));
        solution.Save(solutionPath);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
