extern alias styleanalyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharedKernel.Results;

namespace SharedKernel.Style.CodeFixes.Tests;

/// <summary>Tests the preview, compilation and document Fix All for a guarded Result.Map projection.</summary>
public sealed class ResultProjectionCodeFixTests
{
    private const string DiagnosticId = "SKSTYLE019";
    private const string Source = """
        using SharedKernel.Results;
        public sealed class Example
        {
            public Result<int> First(bool accepted, int value)
            {
                var input = accepted ? Result.Accepted(value) : Result.Conflict<int>("conflict");
                if (input.IsFailure)
                {
                    return input.ConvertError<int, int>();
                }
                return Result.Ok(input.Value + value);
            }

            public Result<(int, string)> Second(bool accepted)
            {
                var input = accepted ? Result.Created(1) : Result.NotFound<int>("missing");
                if (input.IsFailure)
                    return input.ConvertError<int, (int, string)>();
                var value = input.Value;
                return Result.Ok((value, "projection"));
            }

            public Result<int> Unproven(Result<int> source)
            {
                var input = source;
                if (input.IsFailure)
                    return input.ConvertError<int, int>();
                return Result.Ok(input.Value);
            }
        }
        """;

    [Fact]
    public async Task Single_fix_previews_and_compiles_without_capturing_an_existing_parameter_name()
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

        // Assert
        var action = actions.ShouldHaveSingleItem();
        var preview = await action.GetPreviewOperationsAsync(TestContext.Current.CancellationToken);
        preview.OfType<ApplyChangesOperation>().ShouldHaveSingleItem();
        var original = await workspace.Document.GetTextAsync(TestContext.Current.CancellationToken);
        original.ToString().ShouldContain("return Result.Ok(input.Value + value);", StringComparison.Ordinal);

        await workspace.ApplyCodeAction(action);
        var fixedText = await workspace.Document.GetTextAsync(TestContext.Current.CancellationToken);
        fixedText.ToString().ShouldContain("return input.Map", StringComparison.Ordinal);
        fixedText.ToString().ShouldNotContain("return Result.Ok(input.Value + value);", StringComparison.Ordinal);
        (await workspace.GetCompilationDiagnostics()).Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Document_fix_all_changes_only_the_two_proven_disjoint_guards()
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create(Source, new CodeFixWorkspaceOptions
        {
            AdditionalReferences = [MetadataReference.CreateFromFile(typeof(Result).Assembly.Location)],
            NullableContextOptions = NullableContextOptions.Enable,
        });
        var provider = new StyleCodeFixProvider();
        var analyzer = new styleanalyzers::SharedKernel.Style.Analyzers.StyleAnalyzer();
        var diagnostics = (await workspace.GetAnalyzerDiagnostics(analyzer, DiagnosticId))
            .Where(static diagnostic => diagnostic.Id == DiagnosticId).ToArray();

        // Act
        diagnostics.Length.ShouldBe(2);
        var actions = await workspace.GetCodeActions(provider, diagnostics[0]);
        var context = new FixAllContext(workspace.Document, provider, FixAllScope.Document,
            actions.ShouldHaveSingleItem().EquivalenceKey, [DiagnosticId], new ResultProjectionFixAllDiagnostics(diagnostics),
            TestContext.Current.CancellationToken);
        var fix = await provider.GetFixAllProvider().GetFixAsync(context);
        await workspace.ApplyCodeAction(fix.ShouldNotBeNull());

        // Assert
        var syntax = await workspace.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var root = syntax.ShouldNotBeNull();
        root.DescendantNodes().OfType<IfStatementSyntax>().ShouldHaveSingleItem()
            .Condition.ToString().ShouldBe("input.IsFailure");
        var text = (await workspace.Document.GetTextAsync(TestContext.Current.CancellationToken)).ToString();
        text.ShouldContain("return input.ConvertError<int, int>();", StringComparison.Ordinal);
        (await workspace.GetCompilationDiagnostics()).Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Stale_projection_diagnostic_cannot_offer_a_fix_for_an_unproven_result()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;
            public sealed class Example
            {
                public Result<int> Build(Result<int> source)
                {
                    var input = source;
                    if (input.IsFailure)
                        return input.ConvertError<int, int>();
                    return Result.Ok(input.Value);
                }
            }
            """;
        var workspace = CodeFixWorkspace.Create(source, new CodeFixWorkspaceOptions
        {
            AdditionalReferences = [MetadataReference.CreateFromFile(typeof(Result).Assembly.Location)],
        });
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "if (input.IsFailure)");

        // Act
        var actions = await workspace.GetCodeActions(new StyleCodeFixProvider(), diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

}
