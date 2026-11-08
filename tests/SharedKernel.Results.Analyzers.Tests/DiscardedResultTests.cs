namespace PandaBear.Results.Analyzers.Tests;

/// <summary>Tests that the analyzer reports discarded Result values (SKRES002) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DiscardedResultCapability)]
public sealed class DiscardedResultTests
{
    [Theory]
    [InlineData("Run();", "Run")]
    [InlineData("Load();", "Load")]
    [InlineData("RunTask();", "RunTask")]
    [InlineData("await RunTask();", "RunTask")]
    [InlineData("await RunTask().ConfigureAwait(false);", "RunTask")]
    [InlineData("await LoadValueTask();", "LoadValueTask")]
    [InlineData("LoadValueTask();", "LoadValueTask")]
    [InlineData("Result.Ok();", "Ok")]
    public async Task A_discarded_result_is_reported_with_the_method_name(string statement, string methodName)
    {
        // Arrange
        var source = ResultSources.Statements(statement);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ResultsDiagnosticIds.DiscardedResult);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain($"'{methodName}'", StringComparison.Ordinal);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain(statement, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("_ = Run();")]
    [InlineData("_ = await RunTask();")]
    [InlineData("var r = Run(); Use(r);")]
    [InlineData("Use(Run());")]
    [InlineData("if (Run().IsSuccess) { }")]
    [InlineData("var t = RunTask(); await t;")]
    [InlineData("Count();")]
    [InlineData("Task.Delay(1).Wait();")]
    [InlineData("await Task.Yield();")]
    [InlineData("Func<Result> f = Run; _ = f;")]
    public async Task A_result_that_is_used_or_explicitly_discarded_is_not_reported(string statement)
    {
        // Arrange
        var source = ResultSources.Statements(statement);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_returned_result_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Service
            {
                public Result Execute() => Run();

                public Result Other()
                {
                    return Run();
                }

                public async Task<Result> Wait() => await RunTask();

                private static Result Run() => Result.Ok();

                private static Task<Result> RunTask() => Task.FromResult(Result.Ok());
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_discarded_result_from_another_namespace_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Other
            {
                public readonly struct Result
                {
                }

                public static class Work
                {
                    public static Result Do() => default;
                }
            }

            namespace Demo
            {
                public sealed class Service
                {
                    public void Execute() => Other.Work.Do();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
