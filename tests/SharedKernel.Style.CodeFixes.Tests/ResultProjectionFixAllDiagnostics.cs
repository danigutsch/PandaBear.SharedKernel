using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace SharedKernel.Style.CodeFixes.Tests;

/// <summary>Provides the analyzer diagnostics collected for one projection test document.</summary>
/// <param name="diagnostics">The diagnostics attached to the test document.</param>
internal sealed class ResultProjectionFixAllDiagnostics(IReadOnlyList<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
{
    /// <inheritdoc />
    public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

    /// <inheritdoc />
    public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<Diagnostic>>([]);

    /// <inheritdoc />
    public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
}
