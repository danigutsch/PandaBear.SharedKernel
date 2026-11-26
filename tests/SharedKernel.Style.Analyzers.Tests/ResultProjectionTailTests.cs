namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Verifies guards whose return tail would change under an automatic projection rewrite.</summary>
public sealed class ResultProjectionTailTests
{
    [Theory]
    [InlineData("if (input.IsSuccess) return input.ConvertError<int, int>();", "return Result.Ok(input.Value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>(); else { _ = input.Value; }", "return Result.Ok(input.Value);")]
    [InlineData("if (input.IsFailure) return other.ConvertError<int, int>();", "return Result.Ok(input.Value);")]
    [InlineData("if (input.IsFailure) return Result.Ok(0);", "return Result.Ok(input.Value);")]
    [InlineData("if (input.IsFailure) return input;", "return Result.Ok(input.Value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "return input;")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "return Other.Ok(input.Value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "return Result.Ok(1);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "return Result.Ok(/* keep */ input.Value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "var value = /* keep */ input.Value; return Result.Ok(value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "long value = input.Value; return Result.Ok((int)value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "var value = other.Value; return Result.Ok(value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "var value = input.Value + 1; return Result.Ok(value);")]
    [InlineData("if (input.IsFailure) return input.ConvertError<int, int>();", "var value = input.Value; _ = value; return Result.Ok(value);")]
    public async Task Different_early_return_or_guarded_tail_keeps_explicit_control_flow(string guard, string tail)
    {
        // Arrange
        var source = $$"""
            using SharedKernel.Results;

            public static class Other
            {
                public static Result<T> Ok<T>(T value) where T : notnull => Result.Ok(value);
            }

            public static class Example
            {
                public static Result<int> Build(bool accepted)
                {
                    var other = Result.Conflict<int>("other");
                    var input = accepted ? Result.Ok(2) : Result.Conflict<int>("source");
                    {{guard}}
                    {{tail}}
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
