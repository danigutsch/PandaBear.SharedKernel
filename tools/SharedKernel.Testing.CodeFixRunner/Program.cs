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

internal static class Program
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        [new testinganalyzers::SharedKernel.Testing.Analyzers.SharedKernelTestingAnalyzer()];

    private const string DiagnosticId = testinganalyzers::SharedKernel.Testing.Analyzers.TestingDiagnosticIds.XunitTestClassHelperMethod;

    private static readonly SharedKernelTestingCodeFixProvider CodeFixProvider = new();

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync("Usage: dotnet run --project tools/SharedKernel.Testing.CodeFixRunner -- <project-or-solution>").ConfigureAwait(false);
            return 2;
        }

        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        using var workspace = MSBuildWorkspace.Create();

        var targetPath = Path.GetFullPath(args[0]);
        var solution = await OpenSolution(workspace, targetPath).ConfigureAwait(false);
        var fixedCount = 0;

        while (true)
        {
            var diagnostic = await FindNextDiagnostic(solution).ConfigureAwait(false);
            if (diagnostic is null)
            {
                break;
            }

            var document = solution.GetDocument(diagnostic.Location.SourceTree);
            if (document is null)
            {
                await Console.Error.WriteLineAsync($"Skipping diagnostic without document: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                break;
            }

            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None);
            await CodeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

            var action = actions.FirstOrDefault();
            if (action is null)
            {
                await Console.Error.WriteLineAsync($"No code fix available for {diagnostic.Location.GetLineSpan().Path}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}").ConfigureAwait(false);
                break;
            }

            solution = await ApplyAction(workspace, solution, action).ConfigureAwait(false);
            fixedCount++;
        }

        await Console.Out.WriteLineAsync($"Fixed {fixedCount} {DiagnosticId} diagnostic(s).").ConfigureAwait(false);
        return 0;
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

    private static async Task<Diagnostic?> FindNextDiagnostic(Solution solution)
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
                .Where(static candidate => string.Equals(candidate.Id, DiagnosticId, StringComparison.Ordinal))
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

    private static async Task<Solution> ApplyAction(Workspace workspace, Solution solution, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        var changedSolution = await FormatChangedDocuments(solution, applyOperation.ChangedSolution).ConfigureAwait(false);

        if (!workspace.TryApplyChanges(changedSolution))
        {
            throw new InvalidOperationException("Failed to apply code fix changes.");
        }

        return workspace.CurrentSolution;
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
