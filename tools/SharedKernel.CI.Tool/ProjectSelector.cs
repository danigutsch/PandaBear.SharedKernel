using System.Globalization;
using System.IO.Enumeration;

namespace PandaBear.CI.Tool;

/// <summary>
/// Selects the test projects and non-test projects that a change affects, and selects every test project when the
/// change cannot be bounded.
/// </summary>
internal static class ProjectSelector
{
    /// <summary>The <see cref="SelectedTest.SelectedBy"/> entry of a test project that the configuration always selects.</summary>
    public const string AlwaysSelected = "alwaysSelected";

    /// <summary>Checks the configured project lists against the solution.</summary>
    /// <exception cref="InvalidOperationException">
    /// A slice or <c>alwaysSelected</c> entry is not a test project of the solution, or, without a default slice, a solution
    /// test project belongs to no slice.
    /// </exception>
    public static void Validate(CiConfiguration configuration, SolutionGraph graph)
    {
        var known = graph.Projects.ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < configuration.Selection.AlwaysSelected.Length; index++)
        {
            ValidateTestProject(configuration.Selection.AlwaysSelected[index], "$.selection.alwaysSelected[" + Index(index) + "]", known, graph);
        }

        var rules = configuration.Selection.TestPaths ?? [];
        for (var rule = 0; rule < rules.Length; rule++)
        {
            for (var index = 0; index < rules[rule].Tests.Length; index++)
            {
                ValidateTestProject(rules[rule].Tests[index], "$.selection.testPaths[" + Index(rule) + "].tests[" + Index(index) + "]", known, graph);
            }
        }

        for (var slice = 0; slice < configuration.Slices.Length; slice++)
        {
            var projects = configuration.Slices[slice].Projects;
            for (var index = 0; index < projects.Length; index++)
            {
                ValidateTestProject(projects[index], "$.slices[" + Index(slice) + "].projects[" + Index(index) + "]", known, graph);
            }
        }

        if (configuration.DefaultSlice is null)
        {
            var sliced = configuration.Slices.SelectMany(static slice => slice.Projects).ToHashSet(StringComparer.Ordinal);
            var unsliced = graph.Tests.Where(test => !sliced.Contains(test)).Order(StringComparer.Ordinal).ToArray();
            if (unsliced.Length > 0)
            {
                throw CiConfigurationFile.Invalid(
                    "$.slices must list every test project of " + graph.Solution + " when $.defaultSlice is not set. Unlisted: " + string.Join(", ", unsliced) + ".");
            }
        }
    }

    /// <summary>Selects the projects for a change.</summary>
    /// <param name="configuration">The validated configuration.</param>
    /// <param name="graph">The solution.</param>
    /// <param name="changes">The changed paths, or <see langword="null"/> to select every test project.</param>
    /// <param name="fullReason">Why <paramref name="changes"/> is <see langword="null"/>.</param>
    /// <param name="nonInertBuildFile">Why a tracked build file is not inert, or <see langword="null"/> when every one is.</param>
    public static SelectionPlan Select(
        CiConfiguration configuration, SolutionGraph graph, IReadOnlyList<string>? changes, string? fullReason, string? nonInertBuildFile)
    {
        if (changes is null)
        {
            return Full(configuration, graph, fullReason ?? "Every test project is selected.");
        }

        if (changes.Count == 0)
        {
            return Plan(configuration, false, true, ["No path changed between the base and HEAD."], [], []);
        }

        var reasons = new List<string>();
        var owners = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var mapped = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var path in changes.Distinct(StringComparer.Ordinal))
        {
            if (Match(configuration.Selection.FullValidation, path) is { } pattern)
            {
                return Full(configuration, graph, path + ": matches the full-validation pattern " + pattern + ".");
            }

            var documentation = Match(configuration.Selection.Documentation, path);
            if (documentation is not null && nonInertBuildFile is null)
            {
                reasons.Add(path + ": matches the documentation pattern " + documentation + ".");
                continue;
            }

            if ((configuration.Selection.TestPaths ?? []).FirstOrDefault(rule => Match(rule.Paths, path) is not null) is { } testPath)
            {
                mapped[path] = testPath.Tests;
                reasons.Add(path + ": selects " + string.Join(", ", testPath.Tests) + " through $.selection.testPaths.");
                continue;
            }

            if (graph.Uncertainty is { } uncertainty)
            {
                return Full(configuration, graph, path + ": changed, and " + uncertainty);
            }

            var owning = graph.Owners(path);
            if (owning.Length == 0)
            {
                return Full(configuration, graph, documentation is null
                    ? path + ": no solution project owns it."
                    : path + ": documentation patterns apply only while every tracked build file is inert, and " + nonInertBuildFile);
            }

            owners[path] = owning;
            reasons.Add(path + ": owned by " + string.Join(", ", owning) + ".");
        }

        if (owners.Count == 0 && mapped.Count == 0)
        {
            return Plan(configuration, false, true, [.. reasons], [], []);
        }

        var changed = owners.Values.SelectMany(static projects => projects).ToHashSet(StringComparer.Ordinal);
        var affected = graph.WithDependents(changed);
        var always = configuration.Selection.AlwaysSelected.ToHashSet(StringComparer.Ordinal);
        var tests = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var test in affected.Where(graph.Tests.Contains).Concat(always).Concat(mapped.Values.SelectMany(static projects => projects)).Distinct(StringComparer.Ordinal))
        {
            var closure = graph.Closure(test);
            string[] selectedBy =
            [
                .. owners.Where(owner => owner.Value.Any(closure.Contains)).Select(static owner => owner.Key),
                .. mapped.Where(path => path.Value.Contains(test, StringComparer.Ordinal)).Select(static path => path.Key),
            ];
            tests[test] = always.Contains(test) ? [.. selectedBy, AlwaysSelected] : selectedBy;
        }

        if (configuration.Selection.RequireCoveringTests)
        {
            var closures = tests.Keys.Select(graph.Closure).ToArray();
            var uncovered = changed.Order(StringComparer.Ordinal).FirstOrDefault(project => !closures.Any(closure => closure.Contains(project)));
            if (uncovered is not null)
            {
                return Full(configuration, graph, uncovered + ": changed, and no selected test project depends on it.");
            }
        }

        foreach (var project in affected.Except(changed).Order(StringComparer.Ordinal))
        {
            reasons.Add(project + ": depends on a changed project.");
        }

        string[] build = [.. affected.Where(project => !graph.Tests.Contains(project)).Order(StringComparer.Ordinal)];
        return Plan(configuration, false, false, [.. reasons], build, tests);
    }

    /// <summary>Returns the first pattern that matches <paramref name="path"/> case-sensitively, or <see langword="null"/>.</summary>
    public static string? Match(IEnumerable<string> patterns, string path) =>
        patterns.FirstOrDefault(pattern => FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false));

    private static SelectionPlan Full(CiConfiguration configuration, SolutionGraph graph, string reason)
    {
        // A listed project that cannot be read is still run, so its failure shows in its slice.
        var tests = graph.Tests.Concat(configuration.Slices.SelectMany(static slice => slice.Projects))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(static test => test, static _ => Array.Empty<string>(), StringComparer.Ordinal);
        return Plan(configuration, true, false, ["Full validation: " + reason], graph.Projects, tests);
    }

    private static SelectionPlan Plan(
        CiConfiguration configuration,
        bool full,
        bool documentationOnly,
        string[] reasons,
        string[] build,
        Dictionary<string, string[]> tests)
    {
        var sliced = configuration.Slices.SelectMany(static slice => slice.Projects).ToHashSet(StringComparer.Ordinal);
        var slices = configuration.Slices.Select(slice =>
        {
            SelectedTest[] selected = [.. tests
                .Where(test => slice.Projects.Contains(test.Key, StringComparer.Ordinal) || (slice.Name == configuration.DefaultSlice && !sliced.Contains(test.Key)))
                .OrderBy(static test => test.Key, StringComparer.Ordinal)
                .Select(static test => new SelectedTest(test.Key, test.Value))];
            return new SliceSelection(slice.Name, selected.Length > 0, selected);
        }).ToArray();
        return new SelectionPlan(string.Empty, null, full, build.Length > 0 || tests.Count > 0, documentationOnly, reasons, build, slices);
    }

    private static void ValidateTestProject(string project, string path, HashSet<string> known, SolutionGraph graph)
    {
        if (!known.Contains(project))
        {
            throw CiConfigurationFile.Invalid(path + " names " + project + ", which is not a project of " + graph.Solution + ".");
        }

        // A project that cannot be read leaves the graph uncertain, which selects every test project.
        if (!graph.Tests.Contains(project) && !graph.Unread.Contains(project))
        {
            throw CiConfigurationFile.Invalid(path + " names " + project + ", which is not a test project.");
        }
    }

    private static string Index(int index) => index.ToString(CultureInfo.InvariantCulture);
}
