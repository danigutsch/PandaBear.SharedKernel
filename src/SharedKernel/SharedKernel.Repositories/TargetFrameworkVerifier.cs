using System.Globalization;
using System.IO.Enumeration;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PandaBear.IO;

namespace PandaBear.Repositories;

/// <summary>
/// Audits the target frameworks of the tracked project files against a consumer-owned policy, without building. The
/// properties are read by <see cref="TargetFrameworkEvaluator"/>, whose limits apply.
/// </summary>
public static partial class TargetFrameworkVerifier
{
    /// <summary>
    /// The largest lock file, global.json, or command file, in bytes, that the tool reads.
    /// </summary>
    internal const int MaxFileBytes = 16 * 1024 * 1024;

    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    /// <summary>
    /// Checks the tracked projects against the <c>targetFrameworks</c> policy of the consumer configuration under
    /// <paramref name="rootPath"/>; see <see cref="Verify(string, TargetFrameworkConfiguration, CancellationToken)"/> for the findings.
    /// </summary>
    /// <param name="rootPath">The repository root.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The deviations, or none when the projects follow the policy.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// The consumer configuration is invalid or has no targetFrameworks section, or Git cannot list the index.
    /// </exception>
    /// <exception cref="TimeoutException">Git does not list the index in time.</exception>
    public static async Task<IReadOnlyList<VerificationIssue>> Verify(string rootPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        var configuration = await ConsumerConfigurationFile.Read(rootPath, cancellationToken).ConfigureAwait(false);
        var targetFrameworks = configuration.TargetFrameworks
            ?? throw new InvalidOperationException($"{ConsumerConfigurationFile.RelativePath} has no targetFrameworks section.");
        return await Verify(rootPath, targetFrameworks, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports each deviation from the policy: first, for each project in ordinal path order, a missing rule or, in this
    /// order, an unreadable property, a <c>TargetFramework</c> that overrides <c>TargetFrameworks</c>, declared frameworks
    /// that differ from the rule, a wrong <c>RollForward</c>, and a lock file for other frameworks; then the frameworks
    /// that the pinned SDK cannot build, by framework; then the <c>dotnet run --project</c> lines that need
    /// <c>--framework</c>, by file and line. A project with an unreadable property has no further findings. A finding
    /// that repeats, such as an unreadable property in a shared props file, is reported once.
    /// </summary>
    /// <param name="rootPath">The repository root.</param>
    /// <param name="configuration">The policy to check the projects against.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The deviations, or none when the projects follow the policy.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Git cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not list the index in time.</exception>
    public static async Task<IReadOnlyList<VerificationIssue>> Verify(
        string rootPath,
        TargetFrameworkConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(configuration);
        var root = Path.GetFullPath(rootPath);
        var tracked = await GitTrackedFiles.Read(root, ["."], cancellationToken).ConfigureAwait(false);
        var trackedSet = tracked.ToHashSet(StringComparer.Ordinal);
        var projects = tracked.Where(static path => ProjectExtensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal))).Order(StringComparer.Ordinal).ToArray();
        var buildFiles = tracked.Where(static path => IsBuildFile(path));
        var readPaths = projects.Concat(buildFiles);
        if (configuration.FrameworkProperties is { } propertiesFile)
        {
            readPaths = readPaths.Append(propertiesFile);
        }

        var files = await MsBuildFileSet.Read(root, readPaths.Distinct(StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);

        List<VerificationIssue> issues = [];
        var frameworkProperties = ReadFrameworkProperties(configuration, files, issues);
        Dictionary<string, IReadOnlyList<string>> effectiveByProject = new(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            var rule = configuration.Projects.FirstOrDefault(rule => Matches(rule.Include, project));
            if (rule is null)
            {
                issues.Add(new VerificationIssue(project, "No targetFrameworks.projects rule includes this project."));
                continue;
            }

            if (ResolveRule(rule, configuration.FrameworkProperties, frameworkProperties, issues) is not { } resolved)
            {
                continue;
            }

            rule = rule with { Frameworks = resolved };

            var evaluation = TargetFrameworkEvaluator.Evaluate(files, project, rule.RollForward is not null);
            issues.AddRange(evaluation.Issues);
            if (evaluation.Issues.Count > 0)
            {
                continue;
            }

            var effective = Check(project, rule, evaluation, issues);
            if (effective.Count > 0)
            {
                effectiveByProject[project] = effective;
                await CheckLock(root, project, trackedSet, effective, issues, cancellationToken).ConfigureAwait(false);
            }
        }

        if (trackedSet.Contains("global.json"))
        {
            await CheckSdk(root, projects, effectiveByProject, issues, cancellationToken).ConfigureAwait(false);
        }

        await CheckCommandFiles(root, configuration, tracked, projects, effectiveByProject, issues, cancellationToken).ConfigureAwait(false);

        if (configuration.FrameworkProperties is not null)
        {
            await CheckFrameworkNames(root, configuration.FrameworkProperties, frameworkProperties, tracked, issues, cancellationToken).ConfigureAwait(false);
        }

        HashSet<(string, string)> seen = [];
        return issues.Where(issue => seen.Add((issue.Path, issue.Message))).ToArray();
    }

    // Evaluates the framework properties file and reports a property list that is not unique and ascending. Returns null when no file is configured.
    private static IReadOnlyDictionary<string, string>? ReadFrameworkProperties(
        TargetFrameworkConfiguration configuration,
        MsBuildFileSet files,
        List<VerificationIssue> issues)
    {
        if (configuration.FrameworkProperties is not { } path)
        {
            return null;
        }

        if (files.Find(path, out var failure) is null)
        {
            issues.Add(new VerificationIssue(path, failure ?? "Is missing from the working tree."));
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var properties = TargetFrameworkEvaluator.EvaluateProperties(files, path);
        var referenced = configuration.Projects
            .SelectMany(static rule => rule.Frameworks)
            .Select(TargetFrameworkConfiguration.PropertyName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);
        foreach (var name in referenced)
        {
            if (properties.TryGetValue(name, out var value) && !IsAscendingAndUnique(Split(value)))
            {
                issues.Add(new VerificationIssue(path, $"Property '{name}' lists '{value}'; its frameworks must be unique and ascending."));
            }
        }

        return properties;
    }

    // Replaces each $(Name) framework of the rule with the frameworks that the property lists; null when a property is undefined.
    private static List<string>? ResolveRule(
        TargetFrameworkRule rule,
        string? propertiesFile,
        IReadOnlyDictionary<string, string>? properties,
        List<VerificationIssue> issues)
    {
        List<string> resolved = [];
        var complete = true;
        foreach (var framework in rule.Frameworks)
        {
            if (TargetFrameworkConfiguration.PropertyName(framework) is not { } name)
            {
                resolved.Add(framework);
            }
            else if (properties is not null && properties.TryGetValue(name, out var value) && Split(value) is { Length: > 0 } listed)
            {
                resolved.AddRange(listed);
            }
            else
            {
                complete = false;
                issues.Add(new VerificationIssue(
                    ConsumerConfigurationFile.RelativePath,
                    $"The rule '{rule.Include}' names '{framework}', which {propertiesFile ?? "frameworkProperties"} does not define."));
            }
        }

        return complete ? resolved : null;
    }

    private static string[] Split(string value) =>
        value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool IsAscendingAndUnique(string[] frameworks)
    {
        for (var index = 1; index < frameworks.Length; index++)
        {
            if (CompareFrameworks(frameworks[index - 1], frameworks[index]) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int CompareFrameworks(string left, string right)
    {
        var leftMatch = Modern().Match(left);
        var rightMatch = Modern().Match(right);
        if (leftMatch.Success && rightMatch.Success)
        {
            var byMajor = int.Parse(leftMatch.Groups[1].Value, CultureInfo.InvariantCulture).CompareTo(int.Parse(rightMatch.Groups[1].Value, CultureInfo.InvariantCulture));
            if (byMajor != 0)
            {
                return byMajor;
            }
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private static async Task CheckFrameworkNames(
        string root,
        string propertiesFile,
        IReadOnlyDictionary<string, string>? properties,
        IReadOnlyList<string> tracked,
        List<VerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        foreach (var file in tracked.Where(path => path != propertiesFile && IsScanned(path)).Order(StringComparer.Ordinal))
        {
            var (content, _) = await Read(root, file, cancellationToken).ConfigureAwait(false);
            if (content is null)
            {
                continue;
            }

            foreach (var (line, name) in FrameworkNameScanner.Scan(Encoding.UTF8.GetString(content)))
            {
                var baseName = name.Split('-')[0];
                var property = properties?.FirstOrDefault(entry => Split(entry.Value).Contains(baseName, StringComparer.OrdinalIgnoreCase)).Key;
                var use = property is null ? "a property" : $"$({property})";
                issues.Add(new VerificationIssue(
                    file,
                    string.Create(CultureInfo.InvariantCulture, $"Line {line}: Names .NET framework '{name}'; use {use} from {propertiesFile}.")));
            }
        }
    }

    private static bool IsScanned(string path)
    {
        if (path == ConsumerConfigurationFile.RelativePath)
        {
            return true;
        }

        string[] extensions = [".csproj", ".fsproj", ".vbproj", ".props", ".targets", ".nuspec", ".sh", ".ps1"];
        return extensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal))
            || (path.StartsWith(".github/", StringComparison.Ordinal)
                && (path.EndsWith(".yml", StringComparison.Ordinal) || path.EndsWith(".yaml", StringComparison.Ordinal)));
    }

    private static bool IsBuildFile(string path)
    {
        var name = path[(path.LastIndexOf('/', StringComparison.Ordinal) + 1)..];
        return name is "Directory.Build.props" or "Directory.Build.targets";
    }

    private static bool Matches(string pattern, string path) => FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false);

    private static string Join(IEnumerable<string> frameworks) => string.Join(';', frameworks);

    // Reports the shadowing, framework, and RollForward findings of one project and returns the frameworks that it builds.
    private static IReadOnlyList<string> Check(string project, TargetFrameworkRule rule, TargetFrameworkEvaluation evaluation, List<VerificationIssue> issues)
    {
        var listed = evaluation.Listed();
        if (evaluation.TargetFramework.Length > 0 && listed.Count > 0)
        {
            issues.Add(new VerificationIssue(
                project,
                $"TargetFramework '{evaluation.TargetFramework}' from {evaluation.TargetFrameworkFile} overrides TargetFrameworks '{evaluation.TargetFrameworks}', so the project builds only {evaluation.TargetFramework}; clear it with <TargetFramework></TargetFramework> where TargetFrameworks is set."));
        }

        var declared = evaluation.Declared();
        if (declared.Count == 0)
        {
            issues.Add(new VerificationIssue(project, "Declares no target framework."));
            return [];
        }

        if (!declared.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(rule.Frameworks))
        {
            issues.Add(new VerificationIssue(project, $"Declares {Join(declared)}, but the rule '{rule.Include}' expects {Join(rule.Frameworks)}."));
        }

        if (rule.RollForward is not null)
        {
            var actual = evaluation.RollForward.Length == 0 ? "Minor" : evaluation.RollForward;
            if (!actual.Equals(rule.RollForward, StringComparison.OrdinalIgnoreCase))
            {
                var shown = evaluation.RollForward.Length == 0 ? "Minor (default)" : evaluation.RollForward;
                issues.Add(new VerificationIssue(project, $"RollForward is '{shown}', but the rule '{rule.Include}' expects '{rule.RollForward}'."));
            }
        }

        return evaluation.Effective();
    }

    private static async Task CheckLock(
        string root,
        string project,
        HashSet<string> tracked,
        IReadOnlyList<string> effective,
        List<VerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        var separator = project.LastIndexOf('/', StringComparison.Ordinal);
        var path = separator < 0 ? "packages.lock.json" : project[..separator] + "/packages.lock.json";
        if (!tracked.Contains(path))
        {
            return;
        }

        var (content, failure) = await Read(root, path, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            issues.Add(new VerificationIssue(path, failure!));
            return;
        }

        var locked = LockFileFrameworks.Read(RepositoryFile.WithoutUtf8ByteOrderMark(content));
        if (locked is null)
        {
            issues.Add(new VerificationIssue(path, "Is not a lock file."));
            return;
        }

        var built = effective.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!built.SetEquals(locked))
        {
            issues.Add(new VerificationIssue(path, $"Locks {Join(locked)}, but {project} builds {Join(effective)}; restore without --locked-mode to regenerate it."));
        }
    }

    private static async Task CheckSdk(
        string root,
        string[] projects,
        Dictionary<string, IReadOnlyList<string>> effectiveByProject,
        List<VerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        var (content, _) = await Read(root, "global.json", cancellationToken).ConfigureAwait(false);
        if (content is null || SdkVersion(content) is not { } version || Major(version) is not { } sdkMajor)
        {
            return;
        }

        SortedDictionary<string, (int Major, string Project)> required = new(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects.Where(effectiveByProject.ContainsKey))
        {
            foreach (var framework in effectiveByProject[project])
            {
                var match = Modern().Match(framework);
                if (match.Success
                    && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var major and >= 5
                    && major > sdkMajor)
                {
                    required.TryAdd(framework, (major, project));
                }
            }
        }

        foreach (var (framework, (major, project)) in required)
        {
            issues.Add(new VerificationIssue(
                "global.json",
                string.Create(CultureInfo.InvariantCulture, $"SDK {version} cannot build {framework} (first required by {project}); pin an SDK {major}.0.100 or later.")));
        }
    }

    private static string? SdkVersion(byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(
                RepositoryFile.WithoutUtf8ByteOrderMark(content),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("sdk", out var sdk)
                && sdk.ValueKind == JsonValueKind.Object
                && sdk.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.String
                ? version.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? Major(string version)
    {
        var digits = version.TakeWhile(char.IsAsciiDigit).ToArray();
        return digits.Length > 0 && int.TryParse(digits, CultureInfo.InvariantCulture, out var major) ? major : null;
    }

    private static async Task CheckCommandFiles(
        string root,
        TargetFrameworkConfiguration configuration,
        IReadOnlyList<string> tracked,
        string[] projects,
        Dictionary<string, IReadOnlyList<string>> effectiveByProject,
        List<VerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        var patterns = configuration.CommandFiles ?? [];
        foreach (var file in tracked.Where(path => patterns.Any(pattern => Matches(pattern, path))).Order(StringComparer.Ordinal))
        {
            var (content, _) = await Read(root, file, cancellationToken).ConfigureAwait(false);
            if (content is null)
            {
                continue;
            }

            foreach (var run in DotnetRunScanner.Scan(Encoding.UTF8.GetString(content)))
            {
                if (run.HasFramework || Resolve(run.Project.TrimEnd('/'), projects, effectiveByProject) is not { Count: > 1 } effective)
                {
                    continue;
                }

                issues.Add(new VerificationIssue(
                    file,
                    string.Create(CultureInfo.InvariantCulture, $"Line {run.Line}: dotnet run --project {run.Project} needs --framework because the project builds {Join(effective)}.")));
            }
        }
    }

    // A path names a project file, or a directory that directly holds exactly one tracked project file.
    private static IReadOnlyList<string>? Resolve(string path, string[] projects, Dictionary<string, IReadOnlyList<string>> effectiveByProject)
    {
        var project = projects.Contains(path, StringComparer.Ordinal)
            ? path
            : projects.Where(candidate => candidate.StartsWith(path + "/", StringComparison.Ordinal) && !candidate.AsSpan(path.Length + 1).Contains('/')).ToArray() is [var only] ? only : null;
        return project is not null && effectiveByProject.TryGetValue(project, out var effective) ? effective : null;
    }

    private static async Task<(byte[]? Content, string? Failure)> Read(string root, string path, CancellationToken cancellationToken)
    {
        try
        {
            var content = await BoundedReader.ReadFile(Path.Combine(root, path), MaxFileBytes, cancellationToken).ConfigureAwait(false);
            return content is null ? (null, "Exceeds the 16 MiB file size limit.") : (content, null);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, "Is missing from the working tree.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "Cannot be read.");
        }
    }

    [GeneratedRegex(@"^net(?:coreapp)?(\d+)\.\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Modern();
}
