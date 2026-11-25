using System.Xml.Linq;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Repository-wide guard tests for the governed literal catalog.</summary>
public sealed class GovernedLiteralRepositoryTests
{
    // It reads the compiler inputs that a Release build of the whole solution records, so test runs that build only
    // the selected projects skip it; repository quality runs it with --explicit only after building the solution.
    [Fact(Explicit = true)]
    public void Library_catalog_has_exact_canonical_owners_and_no_unapproved_aliases()
    {
        // Arrange
        var repository = Testing.RepositoryRoot.Locate("SharedKernel.slnx");
        var projects = XDocument.Load(Path.Combine(repository, "SharedKernel.slnx")).Descendants("Project")
            .Select(project => Path.Combine(repository, project.Attribute("Path")!.Value.Replace('\\', '/')))
            .Where(project => XDocument.Load(project).Descendants("GovernedLiteralProject").Any(value => value.Value == "true")
                || (XDocument.Load(project).Descendants("PackageId")
                    .Any(id => id.Value.StartsWith("PandaBear.SharedKernel.", StringComparison.Ordinal))
                && !XDocument.Load(project).Descendants("IsPackable").Any(value => value.Value == "false")));
        var manifests = new List<string[]>();
        foreach (var project in projects)
        {
            var inventory = Path.Combine(repository, "TestResults", "governed-literals", "Release", Path.GetFileNameWithoutExtension(project));
            Directory.Exists(inventory).ShouldBeTrue($"Build SharedKernel.slnx in Release before running the guard: missing {inventory}");
            manifests.Add([.. Directory.EnumerateFiles(inventory, "*.inputs").Order(StringComparer.Ordinal)]);
        }

        // A project that builds several frameworks records one manifest per framework. Each framework is checked on its
        // own, with every other project's manifest for that framework or, without one, its first.
        var frameworks = manifests.Where(static files => files.Length > 1).SelectMany(static files => files)
            .Select(static file => Path.GetFileNameWithoutExtension(file)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var catalog = File.ReadAllText(Path.Combine(repository, "src", "SharedKernel", "SharedKernel.Style.Analyzers", "GovernedLiterals.txt"));
        var errors = new List<string>();

        // Act
        foreach (var framework in frameworks.DefaultIfEmpty(string.Empty))
        {
            var compilations = manifests.Where(static files => files.Length > 0)
                .Select(files => GovernedLiteralManifest.Compile(files.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file) == framework) ?? files[0]))
                .ToList();
            errors.AddRange(GovernedConstantGuard.Validate(compilations, catalog).Select(error => framework.Length == 0 ? error : framework + ": " + error));
        }

        // Assert
        errors.ShouldBeEmpty();
    }
}
