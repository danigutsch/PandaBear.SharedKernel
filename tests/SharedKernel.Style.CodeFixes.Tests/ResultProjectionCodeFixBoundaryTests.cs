using Microsoft.CodeAnalysis;
using SharedKernel.Results;

namespace SharedKernel.Style.CodeFixes.Tests;

/// <summary>Verifies code fixes preserve existing local names and decline stale diagnostic spans.</summary>
public sealed class ResultProjectionCodeFixBoundaryTests
{
    private const string DiagnosticId = "SKSTYLE019";
    private const string Source = """
        using SharedKernel.Results;

        public static class Example
        {
            public static Result<int> Build(bool accepted, int mappedValue, int mappedValue2)
            {
                var input = accepted ? Result.Ok(2) : Result.Conflict<int>("source");
                if (input.IsFailure)
                    return input.ConvertError<int, int>();
                return Result.Ok(input.Value + mappedValue + mappedValue2);
            }
        }
        """;

    [Fact]
    public async Task Projection_chooses_a_fresh_parameter_after_two_existing_names()
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create(Source, new CodeFixWorkspaceOptions
        {
            AdditionalReferences = [MetadataReference.CreateFromFile(typeof(Result).Assembly.Location)],
            NullableContextOptions = NullableContextOptions.Enable,
        });
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "if (input.IsFailure)");
        var provider = new StyleCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var fixedText = await workspace.Document.GetTextAsync(TestContext.Current.CancellationToken);
        var compilationDiagnostics = await workspace.GetCompilationDiagnostics();

        // Assert
        fixedText.ToString().ShouldContain("mappedValue3 => mappedValue3 + mappedValue + mappedValue2", StringComparison.Ordinal);
        compilationDiagnostics.ShouldNotContain(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData("input.IsFailure")]
    [InlineData("return Result.Ok(input.Value")]
    public async Task Stale_diagnostics_at_other_spans_do_not_offer_a_rewrite(string spanText)
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create(Source, new CodeFixWorkspaceOptions
        {
            AdditionalReferences = [MetadataReference.CreateFromFile(typeof(Result).Assembly.Location)],
            NullableContextOptions = NullableContextOptions.Enable,
        });
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, spanText);
        var provider = new StyleCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }
}
