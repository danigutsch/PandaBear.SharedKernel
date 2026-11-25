using System.Text.Json;
using SharedKernel.IO;

namespace SharedKernel.CI;

/// <summary>Runs Actionlint for a repository or for workflows affected by a verified Git comparison.</summary>
public sealed class WorkflowLint
{
    private const string DefaultWorkflowDirectory = ".github/workflows";
    private const string ActionDirectory = ".github/actions/";
    private const string ForgejoActionDirectory = ".forgejo/actions/";
    private const string ConfigYaml = ".github/actionlint.yaml";
    private const string ConfigYml = ".github/actionlint.yml";
    private readonly CiMachine machine;
    private readonly RepositoryPath workflowDirectory;

    /// <summary>Creates the workflow linter with its Git and executable runner.</summary>
    /// <param name="machine">The process runner and executable resolver.</param>
    /// <param name="workflowDirectory">The repository-relative directory of workflow files.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="workflowDirectory"/> is not a repository-relative path.</exception>
    public WorkflowLint(CiMachine machine, string workflowDirectory = DefaultWorkflowDirectory)
    {
        ArgumentNullException.ThrowIfNull(machine);
        this.machine = machine;
        this.workflowDirectory = RepositoryPath.Parse(workflowDirectory);
    }

    /// <summary>Runs Actionlint and optionally writes the selected scope and outcome to a JSON file.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="baseRevision">The full base object ID, or <see langword="null"/> to use the full workspace.</param>
    /// <param name="headRevision">The full proposed head object ID, or <see langword="null"/> to use the full workspace.</param>
    /// <param name="actionlint">An executable name on PATH, or a path to the executable relative to <paramref name="root"/>.</param>
    /// <param name="reportPath">The JSON report path relative to <paramref name="root"/>, or <see langword="null"/>.</param>
    /// <param name="output">Receives linter diagnostics and the result when there are no workflows to lint.</param>
    /// <param name="ct">Cancels the check.</param>
    /// <exception cref="ArgumentException"><paramref name="root"/> or <paramref name="actionlint"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The linter cannot run or reports a failure.</exception>
    public async Task Run(string root, string? baseRevision, string? headRevision, string actionlint, string? reportPath, TextWriter output, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionlint);
        ArgumentNullException.ThrowIfNull(output);
        root = Path.GetFullPath(root);
        if (!string.IsNullOrEmpty(Path.GetDirectoryName(actionlint)))
        {
            actionlint = Path.GetFullPath(actionlint, root);
        }

        var scope = await Select(root, baseRevision, headRevision, ct).ConfigureAwait(false);
        var passed = false;
        try
        {
            if (scope.Mode == "none")
            {
                await output.WriteLineAsync("No changed workflows to lint.".AsMemory(), ct).ConfigureAwait(false);
            }
            else
            {
                var defaultFull = scope.Mode == "full" && workflowDirectory.Value == DefaultWorkflowDirectory;
                if (scope.Mode == "full" && !defaultFull)
                {
                    scope.Paths = ListWorkflows(root);
                }

                if (defaultFull || scope.Paths.Length > 0)
                {
                    string[] arguments = defaultFull ? ["-color"] : ["-color", "--", .. scope.Paths];
                    await machine.RunStreaming(actionlint, arguments, root, output, ct).ConfigureAwait(false);
                }
            }

            passed = true;
        }
        finally
        {
            if (reportPath is not null)
            {
                await WriteReport(Path.GetFullPath(reportPath, root), scope.Mode, scope.Reason, scope.Paths, passed, ct.IsCancellationRequested)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<(string Mode, string Reason, string[] Paths)> Select(string root, string? baseRevision, string? headRevision, CancellationToken ct)
    {
        if (baseRevision is null && headRevision is null)
        {
            return ("full", "no-comparison", []);
        }

        if (baseRevision is null || headRevision is null || !ProjectSelectionPlan.IsObjectId(baseRevision) || !ProjectSelectionPlan.IsObjectId(headRevision))
        {
            return ("full", "invalid-comparison", []);
        }

        try
        {
            var repository = await machine.RunGit(["rev-parse", "--is-shallow-repository", "--show-prefix"], root, ct).ConfigureAwait(false);
            if (!string.Equals(repository.TrimEnd('\r', '\n'), "false", StringComparison.Ordinal))
            {
                return ("full", "unverified-checkout", []);
            }

            var mergeBase = (await machine.RunGit(["merge-base", "--all", baseRevision, headRevision], root, ct).ConfigureAwait(false)).Trim();
            if (!ProjectSelectionPlan.IsObjectId(mergeBase))
            {
                return ("full", "ambiguous-merge-base", []);
            }

            await machine.RunGit(["merge-base", "--is-ancestor", headRevision, "HEAD"], root, ct).ConfigureAwait(false);
            var workflows = workflowDirectory.Value + '/';
            var diff = await machine.RunGit(["--literal-pathspecs", "diff", "--name-only", "--no-renames", "-z", mergeBase, headRevision, "--",
                workflows, ActionDirectory, ForgejoActionDirectory, ConfigYaml, ConfigYml], root, ct)
                .ConfigureAwait(false);
            var paths = new List<string>();
            foreach (var path in diff.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                if (path is ConfigYaml or ConfigYml || path.StartsWith(ActionDirectory, StringComparison.Ordinal)
                    || path.StartsWith(ForgejoActionDirectory, StringComparison.Ordinal))
                {
                    return ("full", "shared-lint-input", []);
                }

                if (path.StartsWith(workflows, StringComparison.Ordinal) && IsWorkflow(path)
                    && File.Exists(RepositoryPath.Parse(path).ResolveUnder(root)))
                {
                    paths.Add(path);
                }
            }

            return paths.Count == 0 ? ("none", "no-changed-workflows", []) : ("changed", "verified-comparison", [.. paths]);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested && exception is InvalidOperationException or TimeoutException or IOException or UnauthorizedAccessException)
        {
            return ("full", "unverified-comparison", []);
        }
    }

    private string[] ListWorkflows(string root) =>
        [.. Directory.EnumerateFiles(workflowDirectory.ResolveUnder(root), "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false,
            })
            .Where(IsWorkflow)
            .Select(file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)];

    private static bool IsWorkflow(string path) => Path.GetExtension(path) is ".yaml" or ".yml";

    private static async Task WriteReport(string path, string mode, string reason, string[] paths, bool passed, bool cancelled)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        using var writer = new Utf8JsonWriter(file, new JsonWriterOptions { Indented = true, NewLine = "\n" });
        writer.WriteStartObject();
        writer.WriteString("mode", mode);
        writer.WriteString("reason", reason);
        writer.WriteString("outcome", passed ? "passed" : cancelled ? "cancelled" : "failed");
        writer.WriteStartArray("paths");
        foreach (var workflow in paths)
        {
            writer.WriteStringValue(workflow);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        await file.WriteAsync("\n"u8.ToArray(), CancellationToken.None).ConfigureAwait(false);
    }
}
