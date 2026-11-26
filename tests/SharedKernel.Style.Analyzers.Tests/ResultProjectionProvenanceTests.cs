namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Verifies the analyzer leaves cases with uncertain initialization or a different error path explicit.</summary>
public sealed class ResultProjectionProvenanceTests
{
    [Theory]
    [InlineData("Result<int> input = Result.Ok(2), other = Result.Ok(3);")]
    [InlineData("var input = Result.Ok(2); Result<int> unused;")]
    [InlineData("var input = Result.Ok(2); var other = Result.Ok(3);")]
    [InlineData("var input = accepted ? default : Result.Ok(2);")]
    [InlineData("var input = accepted ? LocalFactories.Ok(2) : Result.Ok(3);")]
    [InlineData("var input = LocalFactories.Ok(2);")]
    public async Task Declaration_that_does_not_prove_the_local_result_keeps_the_guard(string initialization)
    {
        // Arrange
        var source = $$"""
            using SharedKernel.Results;

            public static class LocalFactories
            {
                public static Result<T> Ok<T>(T value) where T : notnull => Result.Ok(value);
            }

            public static class Example
            {
                public static Result<int> Build(bool accepted)
                {
                    {{initialization}}
                    if (input.IsFailure)
                        return input.ConvertError<int, int>();
                    return Result.Ok(input.Value);
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Local_initialized_in_an_outer_block_keeps_the_nested_guard()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;

            public static class Example
            {
                public static Result<int> Build(bool requested)
                {
                    var input = Result.Ok(2);
                    if (requested)
                    {
                        if (input.IsFailure)
                            return input.ConvertError<int, int>();
                        return Result.Ok(input.Value);
                    }
                    return Result.Ok(0);
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Projection_that_embeds_the_original_result_keeps_the_guard()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;

            public static class Example
            {
                public static Result<(int, Result<int>)> Build()
                {
                    var input = Result.Ok(2);
                    if (input.IsFailure)
                        return input.ConvertError<int, (int, Result<int>)>();
                    return Result.Ok((input.Value, input));
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("int", "2", "long", "Result.Ok<long>(input.Value)")]
    [InlineData("Task<int>", "Task.FromResult(2)", "Task<int>", "Result.Ok(input.Value)")]
    public async Task Implicit_type_change_or_async_map_keeps_the_guard(
        string sourceType, string value, string outputType, string success)
    {
        // Arrange
        var source = $$"""
            using System.Threading.Tasks;
            using SharedKernel.Results;

            public static class Example
            {
                public static Result<{{outputType}}> Build()
                {
                    var input = Result.Ok({{value}});
                    if (input.IsFailure)
                        return input.ConvertError<{{sourceType}}, {{outputType}}>();
                    return {{success}};
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
