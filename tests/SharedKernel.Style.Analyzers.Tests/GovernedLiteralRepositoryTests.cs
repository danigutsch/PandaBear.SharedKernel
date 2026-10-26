using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Style.Analyzers.Tests;

public sealed class GovernedLiteralRepositoryTests
{
    // It reads the compiler inputs that a Release build of the whole solution records, so test runs that build only
    // the selected projects skip it; repository quality runs it with --explicit only after building the solution.
    [Fact(Explicit = true)]
    public void Library_catalog_has_exact_canonical_owners_and_no_unapproved_aliases()
    {
        // Arrange
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SharedKernel.slnx")))
        {
            root = root.Parent;
        }

        var repository = root.ShouldNotBeNull().FullName;
        var projects = XDocument.Load(Path.Combine(repository, "SharedKernel.slnx")).Descendants("Project")
            .Select(project => Path.Combine(repository, project.Attribute("Path")!.Value.Replace('\\', '/')))
            .Where(project => XDocument.Load(project).Descendants("GovernedLiteralProject").Any(value => value.Value == "true")
                || (XDocument.Load(project).Descendants("PackageId")
                    .Any(id => id.Value.StartsWith("PandaBear.SharedKernel.", StringComparison.Ordinal))
                && !XDocument.Load(project).Descendants("IsPackable").Any(value => value.Value == "false")));
        var compilations = new List<CSharpCompilation>();
        foreach (var project in projects)
        {
            var inventory = Path.Combine(repository, "TestResults", "governed-literals", "Release", Path.GetFileNameWithoutExtension(project));
            Directory.Exists(inventory).ShouldBeTrue($"Build SharedKernel.slnx in Release before running the guard: missing {inventory}");
            foreach (var manifest in Directory.EnumerateFiles(inventory, "*.inputs"))
            {
                var lines = File.ReadAllLines(manifest).Select(line => line.Split('|', 2)).ToArray();
                var assembly = lines.ShouldHaveSingleItem(line => line[0] == "assembly")[1];
                var defines = lines.ShouldHaveSingleItem(line => line[0] == "defines")[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: defines);
                var trees = lines.Where(line => line[0] == "source").Select(line =>
                    CSharpSyntaxTree.ParseText(File.ReadAllText(line[1]), options, line[1]));
                var references = lines.Where(line => line[0] == "reference").Select(line => MetadataReference.CreateFromFile(line[1]));
                compilations.Add(CSharpCompilation.Create(assembly, trees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
            }
        }

        var catalog = File.ReadAllText(Path.Combine(repository, "src", "SharedKernel", "SharedKernel.Style.Analyzers", "GovernedLiterals.txt"));

        // Act
        var errors = GovernedConstantGuard.Validate(compilations, catalog);

        // Assert
        errors.ShouldBeEmpty();
    }
}
