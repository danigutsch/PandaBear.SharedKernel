using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;

namespace SharedKernel.CodeFixes.MSBuild;

/// <summary>Applies Roslyn code fixes to C# projects loaded through MSBuild.</summary>
public sealed class MSBuildCodeFixRunner
{
    private static readonly Lock MSBuildRegistrationLock = new();

    private readonly ImmutableArray<DiagnosticAnalyzer> analyzers;
    private readonly CodeFixProvider codeFixProvider;

    /// <summary>Creates a runner using the supplied analyzer and code fix provider.</summary>
    /// <param name="analyzer">The analyzer that reports diagnostics in the target.</param>
    /// <param name="codeFixProvider">The provider whose code actions apply a single set of workspace changes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="analyzer"/> or <paramref name="codeFixProvider"/> is null.</exception>
    public MSBuildCodeFixRunner(DiagnosticAnalyzer analyzer, CodeFixProvider codeFixProvider)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(codeFixProvider);

        analyzers = [analyzer];
        this.codeFixProvider = codeFixProvider;
    }

    /// <summary>Diagnostic IDs the code fix provider can fix.</summary>
    public ImmutableArray<string> FixableDiagnosticIds => codeFixProvider.FixableDiagnosticIds;

    /// <summary>Fixes the requested diagnostic in place and formats the changed documents.</summary>
    /// <param name="targetPath">The path of an existing .csproj, .sln, or .slnx file.</param>
    /// <param name="diagnosticId">The diagnostic ID to find and pass to the code fix provider.</param>
    /// <param name="error">Receives load failures and diagnostics without an available fix.</param>
    /// <param name="ct">Cancels loading and fixing. Fixes already written remain on disk.</param>
    /// <returns>
    /// The number of fixed diagnostics, or <see langword="null"/> when MSBuild or the target did not load. Load failures
    /// are written to <paramref name="error"/> and no fix is applied.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> was canceled. Load failures reported after cancellation are not written.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="targetPath"/> has an unsupported file extension.</exception>
    /// <exception cref="InvalidOperationException">The workspace cannot apply the code action or formatting changes.</exception>
    public Task<int?> Run(string targetPath, string diagnosticId, TextWriter error, CancellationToken ct)
    {
        return Run(targetPath, diagnosticId, error, OpenSolution, ct);
    }

    /// <inheritdoc cref="Run(string, string, TextWriter, CancellationToken)"/>
    /// <param name="targetPath">The path of an existing project or solution file.</param>
    /// <param name="diagnosticId">The diagnostic ID to fix.</param>
    /// <param name="error">Receives load failures and diagnostics without an available fix.</param>
    /// <param name="openSolution">Loads the target path into the workspace and returns the loaded solution.</param>
    /// <param name="ct">Cancels loading and fixing.</param>
    internal async Task<int?> Run(
        string targetPath,
        string diagnosticId,
        TextWriter error,
        Func<MSBuildWorkspace, string, CancellationToken, Task<Solution>> openSolution,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(openSolution);

        if (RegisterMSBuild() is { } registrationFailure)
        {
            ct.ThrowIfCancellationRequested();
            await error.WriteLineAsync(registrationFailure.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return null;
        }

        using var workspace = MSBuildWorkspace.Create();
        var solution = await openSolution(workspace, targetPath, ct).ConfigureAwait(false);

        // Ctrl+C and SIGTERM sent to the process group also stop the MSBuild build host. When that happens as loading ends,
        // the workspace reports the lost build host as a load failure instead of throwing.
        ct.ThrowIfCancellationRequested();
        if (await ReportLoadFailures(workspace, error).ConfigureAwait(false))
        {
            return null;
        }

        var initialSolution = solution;
        var fixedCount = 0;
        while (true)
        {
            var fixAttempt = await ApplyFirstFix(workspace, solution, diagnosticId, ct).ConfigureAwait(false);
            if (fixAttempt.ChangedSolution is null)
            {
                var unsupportedDiagnostics = fixAttempt.Diagnostics;

                if (fixedCount > 0)
                {
                    var formatted = await FormatChangedDocuments(initialSolution, solution, ct).ConfigureAwait(false);
                    solution = await ApplyChanges(workspace, solution, formatted, ct).ConfigureAwait(false);
                    if (!unsupportedDiagnostics.IsEmpty)
                    {
                        unsupportedDiagnostics = await FindDiagnostics(solution, diagnosticId, ct).ConfigureAwait(false);
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

    private async Task<FixAttempt> ApplyFirstFix(Workspace workspace, Solution solution, string diagnosticId, CancellationToken ct)
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
                    return new FixAttempt(await ApplyAction(workspace, solution, action, ct).ConfigureAwait(false), []);
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

    /// <summary>Registers MSBuild from a .NET SDK found for the current directory.</summary>
    /// <returns>A one-line error when no SDK provides MSBuild, or <see langword="null"/> when MSBuild can be used.</returns>
    private static string? RegisterMSBuild()
    {
        // Runs in one process, such as parallel tests, must not interleave checking and registering.
        lock (MSBuildRegistrationLock)
        {
            // MSBuild is already registered, or its assemblies are already loaded and cannot be replaced.
            if (!MSBuildLocator.CanRegister)
            {
                return null;
            }

            try
            {
                MSBuildLocator.RegisterDefaults();
                return null;
            }
            catch (InvalidOperationException exception)
            {
                // The message can span lines, such as the .NET host's report of a global.json that names a missing SDK.
                var details = string.Join(' ', exception.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return $"No fixes were applied because MSBuild could not be loaded from a .NET SDK: {details}";
            }
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

    private async Task<ImmutableArray<Diagnostic>> GetProjectDiagnostics(Project project, string diagnosticId, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = await compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync(ct)
            .ConfigureAwait(false);

        return diagnostics
            .Where(candidate => string.Equals(candidate.Id, diagnosticId, StringComparison.Ordinal))
            .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Location.GetLineSpan().StartLinePosition.Line)
            .ToImmutableArray();
    }

    private async Task<ImmutableArray<Diagnostic>> FindDiagnostics(Solution solution, string diagnosticId, CancellationToken ct)
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

    private async Task<CodeAction?> GetFirstCodeAction(Document document, Diagnostic diagnostic, CancellationToken ct)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            ct);
        await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        return actions.FirstOrDefault();
    }

    private static async Task<Solution> ApplyAction(Workspace workspace, Solution solution, CodeAction action, CancellationToken ct)
    {
        var operations = await action.GetOperationsAsync(ct).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        return await ApplyChanges(workspace, solution, applyOperation.ChangedSolution, ct).ConfigureAwait(false);
    }

    private static Task<Solution> ApplyChanges(Workspace workspace, Solution before, Solution after, CancellationToken ct)
    {
        // MSBuildWorkspace adds an unconditional Compile item for a new document. Once we own the disk writes,
        // continue from the last applied solution because the original workspace snapshot has no new document.
        if (!ReferenceEquals(workspace.CurrentSolution, before)
            || after.GetChanges(before).GetProjectChanges().Any(static changes => changes.GetAddedDocuments().Any()))
        {
            return CodeFixFileChanges.Apply(before, after, ct);
        }

        return workspace.TryApplyChanges(after)
            ? Task.FromResult(workspace.CurrentSolution)
            : throw new InvalidOperationException("Failed to apply code fix changes.");
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
