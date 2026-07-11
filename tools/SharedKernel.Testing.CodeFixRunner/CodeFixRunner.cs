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
using SharedKernel.Testing.CodeFixes;

namespace SharedKernel.Testing.CodeFixRunner;

internal static class CodeFixRunEngine
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        [new testinganalyzers::SharedKernel.Testing.Analyzers.SharedKernelTestingAnalyzer()];

    private static readonly SharedKernelTestingCodeFixProvider CodeFixProvider = new();

    public static async Task<int> Run(CodeFixRunnerOptions options, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(error);

        EnsureMSBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolution(workspace, options.TargetPath).ConfigureAwait(false);
        var initialSolution = solution;
        var fixedCount = 0;
        while (true)
        {
            var fixAttempt = await ApplyFirstFix(workspace, solution, options.DiagnosticId).ConfigureAwait(false);
            if (fixAttempt.ChangedSolution is null)
            {
                if (!fixAttempt.Diagnostics.IsEmpty)
                {
                    await ReportUnsupportedDiagnostics(solution, fixAttempt.Diagnostics, error).ConfigureAwait(false);
                }

                if (fixedCount > 0)
                {
                    await FormatAndApplyChanges(workspace, initialSolution, solution).ConfigureAwait(false);
                }

                return fixedCount;
            }

            solution = fixAttempt.ChangedSolution;
            fixedCount++;
        }
    }

    private static async Task<FixAttempt> ApplyFirstFix(Workspace workspace, Solution solution, string diagnosticId)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var project in GetCSharpProjects(solution))
        {
            var projectDiagnostics = await GetProjectDiagnostics(project, diagnosticId).ConfigureAwait(false);
            diagnostics.AddRange(projectDiagnostics);
            foreach (var diagnostic in projectDiagnostics)
            {
                var document = GetDocument(solution, diagnostic);
                if (document is null)
                {
                    continue;
                }

                var action = await GetFirstCodeAction(document, diagnostic).ConfigureAwait(false);
                if (action is not null)
                {
                    return new FixAttempt(await ApplyAction(workspace, action).ConfigureAwait(false), []);
                }
            }
        }

        return new FixAttempt(null, diagnostics.ToImmutable());
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

    private static async Task<Solution> OpenSolution(MSBuildWorkspace workspace, string targetPath)
    {
        return Path.GetExtension(targetPath) switch
        {
            ".csproj" => (await workspace.OpenProjectAsync(targetPath).ConfigureAwait(false)).Solution,
            ".sln" or ".slnx" => await workspace.OpenSolutionAsync(targetPath).ConfigureAwait(false),
            _ => throw new ArgumentException("Expected a .csproj, .sln, or .slnx path.", nameof(targetPath)),
        };
    }

    private static IEnumerable<Project> GetCSharpProjects(Solution solution) =>
        solution.Projects
            .Where(static project => project.Language == LanguageNames.CSharp)
            .OrderBy(static project => project.FilePath, StringComparer.Ordinal);

    private static async Task<ImmutableArray<Diagnostic>> GetProjectDiagnostics(Project project, string diagnosticId)
    {
        var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = await compilation
            .WithAnalyzers(Analyzers)
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        return diagnostics
            .Where(candidate => string.Equals(candidate.Id, diagnosticId, StringComparison.Ordinal))
            .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Location.GetLineSpan().StartLinePosition.Line)
            .ToImmutableArray();
    }

    private readonly record struct FixAttempt(Solution? ChangedSolution, ImmutableArray<Diagnostic> Diagnostics);

    private static async Task<CodeAction?> GetFirstCodeAction(Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await CodeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        return actions.FirstOrDefault();
    }

    private static async Task<Solution> ApplyAction(Workspace workspace, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        return workspace.TryApplyChanges(applyOperation.ChangedSolution)
            ? workspace.CurrentSolution
            : throw new InvalidOperationException("Failed to apply code fix changes.");
    }

    private static async Task FormatAndApplyChanges(Workspace workspace, Solution initialSolution, Solution changedSolution)
    {
        var formattedSolution = await FormatChangedDocuments(initialSolution, changedSolution).ConfigureAwait(false);
        if (!workspace.TryApplyChanges(formattedSolution))
        {
            throw new InvalidOperationException("Failed to apply formatted code fix changes.");
        }
    }

    private static async Task<Solution> FormatChangedDocuments(Solution oldSolution, Solution newSolution)
    {
        foreach (var projectChanges in newSolution.GetChanges(oldSolution).GetProjectChanges())
        {
            foreach (var documentId in projectChanges.GetChangedDocuments())
            {
                var document = newSolution.GetDocument(documentId);
                if (document is null)
                {
                    continue;
                }

                document = await Formatter.FormatAsync(document).ConfigureAwait(false);
                newSolution = document.Project.Solution;
            }
        }

        return newSolution;
    }
}
