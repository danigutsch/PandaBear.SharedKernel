using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedHarness = SharedKernel.Testing.Roslyn.GeneratorTestHarness;

namespace SharedKernel.Domain.GeneratorTests;

/// <summary>Runs one tracked driver across a single syntax-tree replacement and checks its generated output.</summary>
internal static class DomainGeneratorIncrementalBehaviorTestSupport
{
    private const string DomainUsing = "using SharedKernel.Domain;\n";

    /// <summary>Runs the generator before and after an edit, keeping an optional second tree unchanged.</summary>
    /// <param name="generator">The generator under test.</param>
    /// <param name="originalSource">The original source of the tree to edit.</param>
    /// <param name="updatedSource">The updated source of the edited tree.</param>
    /// <param name="stableSource">The optional source of a separate unchanged tree.</param>
    /// <returns>Tracked results for both runs.</returns>
    internal static (GeneratorDriverRunResult Before, GeneratorDriverRunResult After) RunAcrossEdit(
        IIncrementalGenerator generator,
        string originalSource,
        string updatedSource,
        string? stableSource = null)
    {
        var compilation = GeneratorTestHarness.CreateCompilation(originalSource);
        var originalTree = compilation.SyntaxTrees.ShouldHaveSingleItem();
        var options = (CSharpParseOptions)originalTree.Options;
        var ct = TestContext.Current.CancellationToken;
        if (stableSource is not null)
        {
            compilation = compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(DomainUsing + stableSource, options, "StableModel.cs", cancellationToken: ct));
        }

        var updatedTree = CSharpSyntaxTree.ParseText(DomainUsing + updatedSource, options, originalTree.FilePath, cancellationToken: ct);
        var updated = compilation.ReplaceSyntaxTree(originalTree, updatedTree);
        var driver = SharedHarness.CreateDriver([generator], options, trackIncrementalGeneratorSteps: true)
            .RunGenerators(compilation, ct);
        return (driver.GetRunResult(), driver.RunGenerators(updated, ct).GetRunResult());
    }

    /// <summary>Asserts that only the edited model requires new source output.</summary>
    /// <param name="before">The original generator run.</param>
    /// <param name="after">The run after the model edit.</param>
    /// <param name="changedHint">The generated file for the edited model.</param>
    /// <param name="stableHint">The generated file for the unchanged model.</param>
    internal static void AssertIndependentOutputs(
        GeneratorDriverRunResult before,
        GeneratorDriverRunResult after,
        string changedHint,
        string stableHint)
    {
        before.Diagnostics.ShouldBeEmpty("Both original model declarations are supported.");
        after.Diagnostics.ShouldBeEmpty("An edit to a supported identity type remains valid.");
        GeneratorTestHarness.GetGeneratedSource(after, changedHint)
            .ShouldNotBe(GeneratorTestHarness.GetGeneratedSource(before, changedHint), userMessage: "The edit changes one model's generated text.");
        GeneratorTestHarness.GetGeneratedSource(after, stableHint)
            .ShouldBe(GeneratorTestHarness.GetGeneratedSource(before, stableHint), userMessage: "The untouched model retains its generated text.");

        var reasons = SourceOutputReasons(after);
        reasons.Length.ShouldBe(2, "Each model should have its own tracked source output.");
        reasons.ShouldContain(IncrementalStepRunReason.Modified, "The edited model's source output should be regenerated.");
        reasons.ShouldContain(
            static reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            "The unedited model's source output should be reused.");
    }

    /// <summary>Returns the recorded reasons for source-output results, where Roslyn observes emission.</summary>
    /// <param name="result">The tracked results from one run of a single generator.</param>
    /// <returns>The source output run reasons.</returns>
    internal static IncrementalStepRunReason[] SourceOutputReasons(GeneratorDriverRunResult result) =>
        [.. result.Results.ShouldHaveSingleItem().TrackedOutputSteps.Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)];
}
