namespace PandaBear.Results.Analyzers.Tests;

/// <summary>Tests that the analyzer reports minimal API delegates returning a raw Result (SKRES001) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.EndpointResultCapability)]
public sealed class RawResultEndpointTests
{
    [Fact]
    public void Supported_diagnostics_match_descriptor_contract()
    {
        // Arrange
        var analyzer = new SharedKernelResultsAnalyzer();

        // Act
        var diagnosticIds = analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).ToArray();

        // Assert
        diagnosticIds.ShouldBe([ResultsDiagnosticIds.RawResultEndpoint, ResultsDiagnosticIds.DiscardedResult]);
    }

    [Theory]
    [InlineData("app.MapGet(\"/a\", () => Result.Ok());")]
    [InlineData("app.MapPost(\"/a\", () => Result.Ok(1));")]
    [InlineData("app.MapPut(\"/a\", () => Run());")]
    [InlineData("app.MapDelete(\"/a\", () => Load());")]
    [InlineData("app.MapPatch(\"/a\", () => Run());")]
    [InlineData("app.Map(\"/a\", () => Run());")]
    [InlineData("app.MapMethods(\"/a\", [\"GET\"], () => Run());")]
    [InlineData("group.MapGet(\"/a\", () => Run());")]
    [InlineData("app.MapGet(\"/a\", () => { return Run(); });")]
    [InlineData("app.MapGet(\"/a\", () => RunTask());")]
    [InlineData("app.MapGet(\"/a\", () => LoadValueTask());")]
    [InlineData("app.MapGet(\"/a\", async () => await RunTask());")]
    [InlineData("app.MapGet(\"/a\", async () => { await Task.Yield(); return Result.Ok(1); });")]
    [InlineData("app.MapGet(\"/a\", Run);")]
    [InlineData("app.MapGet(\"/a\", RunTask);")]
    [InlineData("app.MapGet(\"/a\", Local); static Result Local() => Result.Ok();")]
    public async Task A_delegate_returning_a_raw_result_is_reported(string body)
    {
        // Arrange
        var source = ResultSources.Endpoints(body);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ResultsDiagnosticIds.RawResultEndpoint);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain(".Map", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lambda_is_reported_at_its_arrow()
    {
        // Arrange
        var source = ResultSources.Endpoints("app.MapGet(\"/a\", () => Run());");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        AnalyzerTestHarness.GetReportedText(diagnostic).ShouldBe("=>");
    }

    [Theory]
    [InlineData("app.MapGet(\"/a\", () => Run().ToHttpResult());")]
    [InlineData("app.MapGet(\"/a\", () => Load().ToHttpResult());")]
    [InlineData("app.MapGet(\"/a\", () => Plain());")]
    [InlineData("app.MapGet(\"/a\", () => Results.Ok(1));")]
    [InlineData("app.MapGet(\"/a\", () => \"text\");")]
    [InlineData("app.MapGet(\"/a\", async () => (await RunTask()).ToHttpResult());")]
    [InlineData("app.MapGet(\"/a\", Plain);")]
    [InlineData("app.MapGet(\"/a\", Local); static IResult Local() => Run().ToHttpResult();")]
    [InlineData("app.MapGet(\"/a\", () => { var r = Run(); });")]
    [InlineData("Func<Result> f = () => Run(); _ = f;")]
    [InlineData("Run();")]
    public async Task A_delegate_that_does_not_return_a_raw_result_is_not_reported(string body)
    {
        // Arrange
        var source = ResultSources.Endpoints(body);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.Where(static d => d.Id == ResultsDiagnosticIds.RawResultEndpoint).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_rule_is_inert_for_a_result_type_from_another_namespace()
    {
        // Arrange
        const string source = """
            namespace Other
            {
                public readonly struct Result
                {
                    public static Result Ok() => default;
                }
            }

            namespace Demo
            {
                public sealed class Endpoints
                {
                    public void Configure(IEndpointRouteBuilder app)
                    {
                        app.MapGet("/a", () => Other.Result.Ok());
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_rules_are_inert_when_the_compilation_does_not_reference_results()
    {
        // Arrange
        const string source = """
            namespace Demo
            {
                public readonly struct Result
                {
                    public static Result Ok() => default;
                }

                public sealed class Endpoints
                {
                    public void Configure(IEndpointRouteBuilder app)
                    {
                        app.MapGet("/a", () => Result.Ok());
                        Result.Ok();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsWithoutResultsReference(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
