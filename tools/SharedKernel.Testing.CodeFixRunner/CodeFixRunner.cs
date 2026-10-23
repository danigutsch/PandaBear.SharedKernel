extern alias testinganalyzers;

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using PandaBear.Testing.CodeFixes;

namespace PandaBear.Testing.CodeFixRunner;

internal static class CodeFixRunEngine
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        [new testinganalyzers::PandaBear.Testing.Analyzers.SharedKernelTestingAnalyzer()];

    private static readonly SharedKernelTestingCodeFixProvider CodeFixProvider = new();

    public static ImmutableArray<string> FixableDiagnosticIds => CodeFixProvider.FixableDiagnosticIds;

    /// <summary>
    /// Fixes the diagnostics with the requested ID in the target and formats the changed documents.
    /// </summary>
    /// <returns>
    /// The number of fixed diagnostics, or <see langword="null"/> when the target did not load. Load failures are written
    /// to <paramref name="error"/> and no fix is applied.
    /// </returns>
    public static async Task<int?> Run(CodeFixRunnerOptions options, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(error);

        EnsureMSBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolution(workspace, options.TargetPath, ct).ConfigureAwait(false);
        if (await ReportLoadFailures(workspace, error).ConfigureAwait(false))
        {
            return null;
        }

        var initialSolution = solution;
        var fixedCount = 0;
        while (true)
        {
            var fixAttempt = await ApplyFirstFix(workspace, solution, options.DiagnosticId, ct).ConfigureAwait(false);
            if (fixAttempt.ChangedSolution is null)
            {
                var unsupportedDiagnostics = fixAttempt.Diagnostics;

                if (fixedCount > 0)
                {
                    await FormatAndApplyChanges(workspace, initialSolution, solution, ct).ConfigureAwait(false);
                    solution = workspace.CurrentSolution;
                    if (!unsupportedDiagnostics.IsEmpty)
                    {
                        unsupportedDiagnostics = await FindDiagnostics(solution, options.DiagnosticId, ct).ConfigureAwait(false);
                    }
                }

                if (!unsupportedDiagnostics.IsEmpty)
                {
                    await ReportUnsupportedDiagnostics(solution, unsupportedDiagnostics, error).ConfigureAwait(false);
                }

                return fixedCount;
            }

            solution = fixAttempt.ChangedSolution;
            fixedCount++;
        }
    }

    private static async Task<FixAttempt> ApplyFirstFix(Workspace workspace, Solution solution, string diagnosticId, CancellationToken ct)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var project in GetCSharpProjects(solution))
        {
            var projectDiagnostics = await GetProjectDiagnostics(project, diagnosticId, ct).ConfigureAwait(false);
            diagnostics.AddRange(projectDiagnostics);
            foreach (var diagnostic in projectDiagnostics)
            {
                var document = GetDocument(solution, diagnostic);
                if (document is null)
                {
                    continue;
                }

                var action = await GetFirstCodeAction(document, diagnostic, ct).ConfigureAwait(false);
                if (action is not null)
                {
                    return new FixAttempt(await ApplyAction(workspace, action, ct).ConfigureAwait(false), []);
                }
            }
        }

        return new FixAttempt(null, diagnostics.ToImmutable());
    }

    private static async Task<bool> ReportLoadFailures(MSBuildWorkspace workspace, TextWriter error)
    {
        // MSBuildWorkspace reports MSBuild errors and warnings, missing projects, and unsupported project types with the
        // same kind, so a harmless warning cannot be told apart from a project loaded without its sources or references.
        var failed = false;
        foreach (var diagnostic in workspace.Diagnostics.Where(static diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure))
        {
            await error.WriteLineAsync(diagnostic.Message).ConfigureAwait(false);
            failed = true;
        }

        if (failed)
        {
            await error.WriteLineAsync("No fixes were applied because the project or solution did not load.").ConfigureAwait(false);
        }

        return failed;
    }

    private static async Task ReportUnsupportedDiagnostics(Solution solution, ImmutableArray<Diagnostic> diagnostics, TextWriter error)
    {
        foreach (var diagnostic in diagnostics)
        {
            var document = GetDocument(solution, diagnostic);
            if (document is null)
            {
                await error.WriteLineAsync($"Skipping diagnostic without document: {diagnostic.Id} at {diagnostic.Location.Kind}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                continue;
            }

            await error.WriteLineAsync(
                $"No code fix available for {diagnostic.Id} at {diagnostic.Location.GetLineSpan().Path}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}")
                .ConfigureAwait(false);
        }
    }

    private static Document? GetDocument(Solution solution, Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree is { } sourceTree
            ? solution.GetDocument(sourceTree)
            : null;
    }

    private static void EnsureMSBuildRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        try
        {
            MSBuildLocator.RegisterDefaults();
        }
        catch (InvalidOperationException)
        {
            // MSBuild can already be loaded by the test host when solution-wide tests run in parallel.
        }
    }

    private static async Task<Solution> OpenSolution(MSBuildWorkspace workspace, string targetPath, CancellationToken ct)
    {
        return Path.GetExtension(targetPath) switch
        {
            ".csproj" => (await workspace.OpenProjectAsync(targetPath, cancellationToken: ct).ConfigureAwait(false)).Solution,
            ".sln" or ".slnx" => await workspace.OpenSolutionAsync(targetPath, cancellationToken: ct).ConfigureAwait(false),
            _ => throw new ArgumentException("Expected a .csproj, .sln, or .slnx path.", nameof(targetPath)),
        };
    }

    private static IEnumerable<Project> GetCSharpProjects(Solution solution) =>
        solution.Projects
            .Where(static project => project.Language == LanguageNames.CSharp)
            .OrderBy(static project => project.FilePath, StringComparer.Ordinal);

    private static async Task<ImmutableArray<Diagnostic>> GetProjectDiagnostics(Project project, string diagnosticId, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = await compilation
            .WithAnalyzers(Analyzers)
            .GetAnalyzerDiagnosticsAsync(ct)
            .ConfigureAwait(false);

        return diagnostics
            .Where(candidate => string.Equals(candidate.Id, diagnosticId, StringComparison.Ordinal))
            .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Location.GetLineSpan().StartLinePosition.Line)
            .ToImmutableArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> FindDiagnostics(Solution solution, string diagnosticId, CancellationToken ct)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var project in GetCSharpProjects(solution))
        {
            diagnostics.AddRange(await GetProjectDiagnostics(project, diagnosticId, ct).ConfigureAwait(false));
        }

        return diagnostics
            .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Location.GetLineSpan().StartLinePosition.Line)
            .ToImmutableArray();
    }

    private readonly record struct FixAttempt(Solution? ChangedSolution, ImmutableArray<Diagnostic> Diagnostics);

    private static async Task<CodeAction?> GetFirstCodeAction(Document document, Diagnostic diagnostic, CancellationToken ct)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            ct);
        await CodeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        return actions.FirstOrDefault();
    }

    private static async Task<Solution> ApplyAction(Workspace workspace, CodeAction action, CancellationToken ct)
    {
        var operations = await action.GetOperationsAsync(ct).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        return workspace.TryApplyChanges(applyOperation.ChangedSolution)
            ? workspace.CurrentSolution
            : throw new InvalidOperationException("Failed to apply code fix changes.");
    }

    private static async Task FormatAndApplyChanges(Workspace workspace, Solution initialSolution, Solution changedSolution, CancellationToken ct)
    {
        var formattedSolution = await FormatChangedDocuments(initialSolution, changedSolution, ct).ConfigureAwait(false);
        if (!workspace.TryApplyChanges(formattedSolution))
        {
            throw new InvalidOperationException("Failed to apply formatted code fix changes.");
        }
    }

    private static async Task<Solution> FormatChangedDocuments(Solution oldSolution, Solution newSolution, CancellationToken ct)
    {
        foreach (var projectChanges in newSolution.GetChanges(oldSolution).GetProjectChanges())
        {
            foreach (var documentId in projectChanges.GetChangedDocuments().Concat(projectChanges.GetAddedDocuments()))
            {
                var document = newSolution.GetDocument(documentId);
                if (document is null)
                {
                    continue;
                }

                document = await Formatter.FormatAsync(document, cancellationToken: ct).ConfigureAwait(false);
                newSolution = document.Project.Solution;
            }
        }

        return newSolution;
    }
}
