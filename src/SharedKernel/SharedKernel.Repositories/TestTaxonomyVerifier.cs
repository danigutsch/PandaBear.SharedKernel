using System.Text.Json;
using PandaBear.IO;

namespace PandaBear.Repositories;

/// <summary>Checks that a consumer declares what each tracked test project covers and where its seam is documented.</summary>
public static class TestTaxonomyVerifier
{
    private const string InventoryPath = ".config/test-taxonomy.json";
    private const int MaximumInventoryBytes = 1024 * 1024;
    private static readonly string[] Types = ["Unit", "Component", "Api", "Contract", "Integration", "System", "Architecture", "Performance"];

    /// <summary>Reports gaps between the typed inventory and the regular projects and registers in the Git index.</summary>
    /// <param name="rootPath">The repository root.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Actionable diagnostics in deterministic order.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">The inventory is unusable or Git cannot list the index.</exception>
    public static async Task<IReadOnlyList<VerificationIssue>> Verify(string rootPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        var root = Path.GetFullPath(rootPath);
        var files = await GitTrackedFiles.Read(root, ["."], cancellationToken).ConfigureAwait(false);
        var tracked = files.ToHashSet(StringComparer.Ordinal);
        if (!tracked.Contains(InventoryPath))
        {
            throw new InvalidOperationException($"Track the repository test taxonomy at {InventoryPath}.");
        }

        var content = await BoundedReader.ReadFile(Path.Combine(root, InventoryPath), MaximumInventoryBytes, cancellationToken).ConfigureAwait(false);
        var inventory = content is null
            ? null
            : JsonSerializer.Deserialize(RepositoryFile.WithoutUtf8ByteOrderMark(content).Span, TestTaxonomyJsonContext.Default.TestTaxonomyInventory);
        if (inventory is null || inventory.Version != 1)
        {
            throw new InvalidOperationException($"{InventoryPath} must be a version 1 inventory no larger than 1 MiB.");
        }

        ValidateRoots(inventory.SutRoots, inventory.TestRoots);
        return Check(inventory, files, tracked);
    }

    private static VerificationIssue[] Check(TestTaxonomyInventory inventory, IReadOnlyList<string> files, HashSet<string> tracked)
    {
        List<VerificationIssue> issues = [];
        if (inventory.Suts.Any(entry => entry is null) || inventory.Suites.Any(suite => suite is null))
        {
            issues.Add(new VerificationIssue(InventoryPath, "Remove null entries from the suts and suites lists."));
        }

        HashSet<string> sutPaths = new(StringComparer.Ordinal);
        foreach (var entry in inventory.Suts)
        {
            if (entry is null || !Register(entry.Path, inventory.SutRoots, tracked, sutPaths, "production SUT", issues))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.DirectOwner))
            {
                issues.Add(new VerificationIssue(entry.Path, "Declare the direct owner of this production SUT."));
            }
        }

        HashSet<string> testPaths = new(StringComparer.Ordinal);
        HashSet<(string Sut, string Type)> coveredTypes = [];
        foreach (var suite in inventory.Suites)
        {
            if (suite is not null && Register(suite.Path, inventory.TestRoots, tracked, testPaths, "test project", issues))
            {
                CheckSuite(suite, tracked, sutPaths, coveredTypes, issues);
            }
        }

        foreach (var project in inventory.SupportProjects)
        {
            Register(project, inventory.TestRoots, tracked, testPaths, "test support project", issues);
        }

        foreach (var entry in inventory.Suts.Where(entry => entry is not null && sutPaths.Contains(entry.Path)))
        {
            foreach (var type in entry.RequiredTypes)
            {
                if (!Types.Contains(type, StringComparer.Ordinal))
                {
                    issues.Add(new VerificationIssue(entry.Path, $"Required test type '{type}' is unsupported. Choose from {string.Join(", ", Types)}."));
                }
                else if (!coveredTypes.Contains((entry.Path, type)))
                {
                    issues.Add(new VerificationIssue(entry.Path, $"Declare a suite for required test type '{type}'."));
                }
            }
        }

        foreach (var path in files.Where(IsProject))
        {
            if (IsUnder(path, inventory.SutRoots) && !sutPaths.Contains(path))
            {
                issues.Add(new VerificationIssue(path, $"Register this production SUT in the suts list in {InventoryPath}."));
            }

            if (IsUnder(path, inventory.TestRoots) && !testPaths.Contains(path))
            {
                issues.Add(new VerificationIssue(path, $"Declare this test project in the suites or supportProjects list in {InventoryPath}."));
            }
        }

        return issues.Distinct().OrderBy(issue => issue.Path, StringComparer.Ordinal).ThenBy(issue => issue.Message, StringComparer.Ordinal).ToArray();
    }

    private static void CheckSuite(
        TestTaxonomySuite suite,
        HashSet<string> tracked,
        HashSet<string> suts,
        HashSet<(string Sut, string Type)> coveredTypes,
        List<VerificationIssue> issues)
    {
        if (!suite.Register.EndsWith(".md", StringComparison.Ordinal) || !tracked.Contains(suite.Register))
        {
            issues.Add(new VerificationIssue(suite.Path, $"The seam register '{suite.Register}' is not a tracked Markdown file. Set register to an existing register or add the file."));
        }

        if (suite.Seams.Length == 0 || suite.Seams.Any(seam => seam is null))
        {
            issues.Add(new VerificationIssue(suite.Path, "Declare at least one seam and remove any null entries."));
            return;
        }

        var declaredSeams = new HashSet<(string Sut, string Type)>();
        foreach (var seam in suite.Seams)
        {
            var knownSut = suts.Contains(seam.Sut);
            var knownType = Types.Contains(seam.Type, StringComparer.Ordinal);
            if (!knownSut)
            {
                issues.Add(new VerificationIssue(suite.Path, $"The SUT '{seam.Sut}' is not registered. Set sut to a production project from the suts list."));
            }

            if (!knownType)
            {
                issues.Add(new VerificationIssue(suite.Path, $"The test type '{seam.Type}' is unsupported. Choose from {string.Join(", ", Types)}."));
            }

            if (string.IsNullOrWhiteSpace(seam.Dependency) || string.IsNullOrWhiteSpace(seam.Lane)
                || seam.Capabilities.Length == 0 || seam.Capabilities.Any(string.IsNullOrWhiteSpace))
            {
                issues.Add(new VerificationIssue(suite.Path, $"Declare a dependency, CI lane, and at least one capability for '{seam.Sut}' ({seam.Type})."));
            }

            if (!declaredSeams.Add((seam.Sut, seam.Type)))
            {
                issues.Add(new VerificationIssue(suite.Path, $"Merge duplicate seam declarations for '{seam.Sut}' ({seam.Type})."));
            }

            if (knownSut && knownType)
            {
                coveredTypes.Add((seam.Sut, seam.Type));
            }
        }

        if (!string.IsNullOrWhiteSpace(suite.MigrationException))
        {
            return;
        }

        if (suite.Seams.Length != 1)
        {
            issues.Add(new VerificationIssue(suite.Path, "Split the project by declared SUT and test type or document its migrationException."));
            return;
        }

        var declared = suite.Seams[0];
        if (suts.Contains(declared.Sut) && Types.Contains(declared.Type, StringComparer.Ordinal))
        {
            var expected = $"{Path.GetFileNameWithoutExtension(declared.Sut)}.Tests.{declared.Type}";
            if (!string.Equals(Path.GetFileNameWithoutExtension(suite.Path), expected, StringComparison.Ordinal))
            {
                issues.Add(new VerificationIssue(suite.Path, $"Rename to '{expected}' or document its migrationException."));
            }
        }
    }

    private static bool Register(
        string? path,
        string[] roots,
        HashSet<string> tracked,
        HashSet<string> registered,
        string kind,
        List<VerificationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsProject(path) || !IsUnder(path, roots) || !tracked.Contains(path))
        {
            issues.Add(new VerificationIssue(InventoryPath, $"The {kind} '{path}' must be a tracked project below its declared roots."));
            return false;
        }

        if (!registered.Add(path))
        {
            issues.Add(new VerificationIssue(path, $"Remove the duplicate {kind} declaration in {InventoryPath}."));
            return false;
        }

        return true;
    }

    private static void ValidateRoots(string[] sutRoots, string[] testRoots)
    {
        if (sutRoots.Length == 0 || testRoots.Length == 0
            || sutRoots.Concat(testRoots).Any(root => string.IsNullOrWhiteSpace(root) || root.Contains('\\', StringComparison.Ordinal)
                || root.Split('/').Any(part => part is "" or "." or ".."))
            || sutRoots.Any(sut => testRoots.Any(test => sut == test || IsUnder(sut, [test]) || IsUnder(test, [sut]))))
        {
            throw new InvalidOperationException($"{InventoryPath} must declare separate, root-relative sutRoots and testRoots with '/' separators.");
        }
    }

    private static bool IsProject(string path) => Path.GetExtension(path) is ".csproj" or ".fsproj" or ".vbproj";

    private static bool IsUnder(string path, string[] roots) => roots.Any(root => path.StartsWith(root + "/", StringComparison.Ordinal));
}
