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

        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolution(workspace, options.TargetPath).ConfigureAwait(false);
        var fixedCount = 0;
        var skippedDiagnostics = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            var diagnostic = await FindNextDiagnostic(solution, options.DiagnosticId, skippedDiagnostics).ConfigureAwait(false);
            if (diagnostic is null)
            {
                return fixedCount;
            }

            var document = solution.GetDocument(diagnostic.Location.SourceTree);
            if (document is null)
            {
                await error.WriteLineAsync($"Skipping diagnostic without document: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                skippedDiagnostics.Add(GetDiagnosticKey(diagnostic));
                continue;
            }

            var action = await GetFirstCodeAction(document, diagnostic).ConfigureAwait(false);
            if (action is null)
            {
                await error.WriteLineAsync($"No code fix available for {diagnostic.Location.GetLineSpan().Path}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}").ConfigureAwait(false);
                skippedDiagnostics.Add(GetDiagnosticKey(diagnostic));
                continue;
            }

            solution = await ApplyAction(workspace, solution, action).ConfigureAwait(false);
            fixedCount++;
        }
    }

    internal static string GetDiagnosticKey(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{lineSpan.Path}:{lineSpan.StartLinePosition.Line}:{lineSpan.StartLinePosition.Character}";
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

    private static async Task<Diagnostic?> FindNextDiagnostic(Solution solution, string diagnosticId, HashSet<string> skippedDiagnostics)
    {
        foreach (var project in solution.Projects.Where(static project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            var diagnostics = await compilation
                .WithAnalyzers(Analyzers)
                .GetAnalyzerDiagnosticsAsync()
                .ConfigureAwait(false);
            var diagnostic = diagnostics
                .Where(candidate => string.Equals(candidate.Id, diagnosticId, StringComparison.Ordinal))
                .Where(candidate => !skippedDiagnostics.Contains(GetDiagnosticKey(candidate)))
                .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Location.GetLineSpan().StartLinePosition.Line)
                .FirstOrDefault();
            if (diagnostic is not null)
            {
                return diagnostic;
            }
        }

        return null;
    }

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

    private static async Task<Solution> ApplyAction(Workspace workspace, Solution solution, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        var changedSolution = await FormatChangedDocuments(solution, applyOperation.ChangedSolution).ConfigureAwait(false);

        return workspace.TryApplyChanges(changedSolution)
            ? workspace.CurrentSolution
            : throw new InvalidOperationException("Failed to apply code fix changes.");
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
