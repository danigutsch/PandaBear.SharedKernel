using System.Text;
using SharedKernel.IO;

namespace SharedKernel.CI;

/// <summary>Verifies the formatting of a checkout using its revision-bound validation plan.</summary>
public sealed class FormatVerifier
{
    private const int MaximumPathManifestBytes = 16 * 1024 * 1024;
    private readonly CiMachine _machine;

    /// <summary>Creates the verifier from the machine that runs Git and dotnet.</summary>
    /// <param name="machine">The process runner and executable resolver.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public FormatVerifier(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>Verifies all solution files for full validation, or the current changed files for selective validation.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="selectionDirectory">The directory containing the validation plan and filters.</param>
    /// <param name="reportDirectory">Receives the formatter's JSON report when it runs, or <see langword="null"/>.</param>
    /// <param name="output">Receives formatter output and the result when no files need formatting.</param>
    /// <param name="ct">Cancels verification.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The plan disagrees with the checkout or formatting fails.</exception>
    public async Task Run(string root, string selectionDirectory, string? reportDirectory, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(selectionDirectory);
        ArgumentNullException.ThrowIfNull(output);

        var selection = await SelectionPlanFile.Read(selectionDirectory, ct).ConfigureAwait(false);
        var revision = (await _machine.RunGit(["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        if (!string.Equals(revision, selection.Revision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The formatting plan revision does not match this checkout.");
        }

        if (!selection.BuildRequired)
        {
            await output.WriteLineAsync("The validation plan requires no project formatting.".AsMemory(), ct).ConfigureAwait(false);
            return;
        }

        string workspace;
        string[] files = [];
        if (selection.FullValidation)
        {
            var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
            workspace = configuration.Solution;
        }
        else
        {
            files = await GetChangedFiles(root, selectionDirectory, selection.ComparisonBase, ct).ConfigureAwait(false);
            if (files.Length == 0)
            {
                await output.WriteLineAsync("No changed files remain in the checkout to format.".AsMemory(), ct).ConfigureAwait(false);
                return;
            }

            workspace = Path.Combine(selectionDirectory, ProjectSelectionPlan.AffectedBuildFilterName);
        }

        List<string> arguments = ["format", workspace, "--verify-no-changes", "--no-restore"];
        if (reportDirectory is not null)
        {
            arguments.AddRange(["--report", Path.GetFullPath(reportDirectory, root)]);
        }

        if (files.Length > 0)
        {
            arguments.AddRange(["--include", .. files]);
        }

        await _machine.RunStreaming("dotnet", [.. arguments], root, output, ct).ConfigureAwait(false);
    }

    private async Task<string[]> GetChangedFiles(string root, string directory, string? comparisonBase, CancellationToken ct)
    {
        if (comparisonBase is null || !ProjectSelectionPlan.IsObjectId(comparisonBase))
        {
            throw new InvalidOperationException("Selective formatting requires a full Git comparison base.");
        }

        await _machine.RunGit(["merge-base", "--is-ancestor", comparisonBase, "HEAD"], root, ct).ConfigureAwait(false);
        var diff = await _machine.RunGit(["diff", "--name-only", "--no-renames", "-z", comparisonBase + "...HEAD", "--"], root, ct)
            .ConfigureAwait(false);
        var paths = diff.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var actual = string.Join('\0', paths) + '\0';
        var planned = await BoundedFile.Read(
            Path.Combine(directory, ProjectSelectionPlan.ChangedPathsFileName), MaximumPathManifestBytes, ProjectSelectionPlan.ChangedPathsFileName, ct)
            .ConfigureAwait(false);
        if (!string.Equals(actual, Encoding.UTF8.GetString(planned), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The formatting plan paths do not match the Git comparison.");
        }

        return [.. paths.Select(RepositoryPath.Parse)
            .Where(path => File.Exists(path.ResolveUnder(root)))
            .Select(static path => "./" + path.Value)];
    }
}
