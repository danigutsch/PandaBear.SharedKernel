using Microsoft.CodeAnalysis;

namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Verifies candidate projections bind to the exact Results contracts and preserve evaluation order.</summary>
public sealed class GuardedResultBindingTests
{
    private const string CanonicalFailure = "if (input.IsFailure) return input.ConvertError<int, int>();";
    private const string ValueSuccess = "return Result.Ok(input.Value);";

    [Theory]
    [InlineData("", "if (supplied.IsFailure) return supplied.ConvertError<int, int>();", "return Result.Ok(supplied.Value);")]
    [InlineData("var input = Result.Ok(2);", "if (requested) if (input.IsFailure) return input.ConvertError<int, int>();", ValueSuccess)]
    [InlineData("var input = new Candidate();", "if (input.IsFailure) return Result.Conflict<int>(\"candidate\");", ValueSuccess)]
    [InlineData("var input = Result.Ok(2);", CanonicalFailure, "return Result.Created(input.Value);")]
    [InlineData("var input = Result.Ok(2);", CanonicalFailure, "_ = input.Value; return Result.Ok(input.Value);")]
    [InlineData("var input = Result.Ok(2);", CanonicalFailure, "var status = input.Status; return Result.Ok((int)status);")]
    [InlineData("var input = Result.Ok(2);", CanonicalFailure, "return Result.Ok(input.Value + Compute());")]
    public async Task Guarded_projection_with_different_binding_or_evaluation_keeps_its_source(
        string initialization, string guard, string tail)
    {
        // Arrange
        var source = $$"""
            using System;
            using SharedKernel.Results;

            public sealed class Candidate
            {
                public bool IsFailure => false;
                public int Value => 2;
            }

            public static class Example
            {
                private static int Compute()
                {
                    Console.WriteLine("computing");
                    return 1;
                }

                public static Result<int> Build(bool requested, Result<int> supplied)
                {
                    {{initialization}}
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

    [Fact]
    public async Task Result_failure_uses_the_existing_consumer_extension()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;

            namespace Consumer;

            public static class AlternateExtensions
            {
                public static Result<TTarget> ConvertError<TSource, TTarget>(this Result<TSource> source)
                    where TSource : notnull
                    where TTarget : notnull => Result.Conflict<TTarget>("consumer conversion");
            }

            public static class Example
            {
                public static Result<long> Build(bool accepted)
                {
                    var input = accepted ? Result.Ok(2) : Result.Conflict<int>("source");
                    if (input.IsFailure)
                        return input.ConvertError<int, long>();
                    return Result.Ok((long)input.Value);
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Built_in_reference_conversion_preserves_the_result_type()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;

            public static class Example
            {
                public static Result<object> Build(bool accepted)
                {
                    var input = accepted ? Result.Ok("value") : Result.Conflict<string>("source");
                    if (input.IsFailure)
                        return input.ConvertError<string, object>();
                    return Result.Ok((object)input.Value);
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Local_function_in_a_top_level_program_keeps_the_guard()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;

            return Build().IsSuccess ? 0 : 1;

            static Result<int> Build()
            {
                var input = Result.Ok(2);
                if (input.IsFailure)
                    return input.ConvertError<int, int>();
                return Result.Ok(input.Value);
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source, outputKind: OutputKind.ConsoleApplication);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
