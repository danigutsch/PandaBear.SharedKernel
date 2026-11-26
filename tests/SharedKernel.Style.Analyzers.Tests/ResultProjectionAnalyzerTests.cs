using SharedKernel.Results;

namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Tests conservative discovery of a guarded Result.Map projection.</summary>
public sealed class ResultProjectionAnalyzerTests
{
    [Theory]
    [InlineData(false, "Ok")]
    [InlineData(false, "Created")]
    [InlineData(true, "Accepted")]
    public async Task Initialized_local_with_typed_failure_and_pure_projection_reports_the_guard(bool valueAlias, string factory)
    {
        // Arrange
        var projection = valueAlias
            ? "var value = input.Value; return Result.Ok((value, 2));"
            : "return Result.Ok((input.Value, 2));";
        var source = $$"""
            using SharedKernel.Results;
            public sealed class Example<T> where T : notnull
            {
                public Result<(T, int)> Build(bool accepted, T candidate)
                {
                    var input = accepted ? Result.{{factory}}(candidate) : Result.Conflict<T>("conflict");
                    if (input.IsFailure)
                    {
                        return input.ConvertError<T, (T, int)>();
                    }
                    {{projection}}
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        text.ToString(diagnostic.Location.SourceSpan).ShouldBe("if");
    }

    [Theory]
    [InlineData("default(Result<int>)", "", "input.Value")]
    [InlineData("untrusted", "", "input.Value")]
    [InlineData("accepted ? Result.Ok(3) : default", "", "input.Value")]
    [InlineData("accepted ? default : Result.Ok(3)", "", "input.Value")]
    [InlineData("Create()", "", "input.Value")]
    [InlineData("accepted ? Result.Ok(3) : Result.Conflict<int>(\"conflict\")", "", "Project(input.Value)")]
    [InlineData("accepted ? Result.Ok(3) : Result.Conflict<int>(\"conflict\")", "", "input.Value + borrowed")]
    [InlineData("accepted ? Result.Ok(3) : Result.Conflict<int>(\"conflict\")", "", "await Task.FromResult(input.Value)")]
    [InlineData("accepted ? Result.Ok(3) : Result.Conflict<int>(\"conflict\")", "// Keep the failure contract explicit.\n", "input.Value")]
    [InlineData("accepted ? Result.Ok(3) : Result.Conflict<int>(\"conflict\")", "Console.WriteLine(1);", "input.Value")]
    public async Task Ambiguous_initialization_projection_or_failure_flow_is_excluded(
        string initializer,
        string failureStatement,
        string projection)
    {
        // Arrange
        var source = $$"""
            using System;
            using System.Threading.Tasks;
            using SharedKernel.Results;
            public sealed class Example
            {
                private static int Project(int input) => input;
                private static Result<int> Create() => Result.Ok(3);
                public Result<int> Build(bool accepted, Result<int> untrusted, ref int borrowed)
                {
                    var input = {{initializer}};
                    if (input.IsFailure)
                    {
                        {{failureStatement}}
                        return input.ConvertError<int, int>();
                    }
                    return Result.Ok({{projection}});
                }
            }
            """;
        if (string.Equals(projection, "await Task.FromResult(input.Value)", StringComparison.Ordinal))
        {
            source = source.Replace("Result<int> Build", "async Task<Result<int>> Build", StringComparison.Ordinal)
                .Replace("ref int borrowed", "int borrowed", StringComparison.Ordinal);
        }

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unrelated_conversion_with_the_same_name_is_excluded()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;
            public sealed class Example
            {
                private static Result<long> ConvertError<T, TOutput>(Result<T> source) where T : notnull
                    => Result.Ok(10L);
                public Result<long> Build(bool accepted)
                {
                    var input = accepted ? Result.Ok(2) : Result.Conflict<int>("conflict");
                    if (input.IsFailure)
                        return ConvertError<int, long>(input);
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
    public async Task String_concatenation_can_call_an_object_formatter_and_is_excluded()
    {
        // Arrange
        const string source = """
            using SharedKernel.Results;
            public sealed class Example
            {
                public Result<string> Build(bool exists, object value)
                {
                    var input = exists ? Result.Ok("prefix") : Result.NotFound<string>("missing");
                    if (input.IsFailure)
                        return input.ConvertError<string, string>();
                    return Result.Ok(input.Value + value);
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("long", "(long)input.Value - offset", true)]
    [InlineData("long", "input.Value + (offset - 1)", true)]
    [InlineData("(int, object)", "(input.Value, (object)text)", true)]
    [InlineData("int", "2", false)]
    [InlineData("int", "input.Value << 1", false)]
    [InlineData("int", "input.Value + Calculate()", false)]
    [InlineData("int", "input.Value + custom", false)]
    public async Task Built_in_value_projection_excludes_opaque_calls_and_user_arithmetic(
        string targetType,
        string projection,
        bool expectedReport)
    {
        // Arrange
        var source = $$"""
            using SharedKernel.Results;
            public sealed class Example
            {
                private static int Calculate() => 3;
                private readonly record struct Offset(int Value)
                {
                    public static int operator +(int value, Offset offset) => value + offset.Value;
                }
                public Result<{{targetType}}> Build(bool exists, string text)
                {
                    var offset = 3L;
                    var custom = new Offset(1);
                    var input = exists ? Result.Ok(2) : Result.NotFound<int>("missing");
                    if (input.IsFailure)
                        return input.ConvertError<int, {{targetType}}>();
                    return Result.Ok({{projection}});
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.Length.ShouldBe(expectedReport ? 1 : 0);
    }

    [Fact]
    public async Task Pure_projection_inside_a_nested_async_block_reports_without_moving_the_await()
    {
        // Arrange
        const string source = """
            using System.Threading.Tasks;
            using SharedKernel.Results;
            public sealed class Example
            {
                public async Task<Result<int>> Build(bool exists, bool requested)
                {
                    await Task.Yield();
                    if (requested)
                    {
                        var input = exists ? Result.Ok(2) : Result.NotFound<int>("missing");
                        if (input.IsFailure)
                            return input.ConvertError<int, int>();
                        return Result.Ok(input.Value + 1);
                    }
                    return Result.Ok(0);
                }
            }
            """;

        // Act
        var diagnostics = await ResultProjectionAnalyzerScenario.Analyze(source);

        // Assert
        diagnostics.ShouldHaveSingleItem();
    }

    [Fact]
    public void Projection_diagnostic_is_opt_in()
    {
        // Arrange
        var analyzer = new StyleAnalyzer();

        // Act
        var rule = analyzer.SupportedDiagnostics.ShouldHaveSingleItem(
            static descriptor => descriptor.Id == StyleDiagnosticIds.GuardedResultProjection);

        // Assert
        rule.IsEnabledByDefault.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ResultStatus.Ok)]
    [InlineData(ResultStatus.Created)]
    [InlineData(ResultStatus.Accepted)]
    [InlineData(ResultStatus.Invalid)]
    [InlineData(ResultStatus.Conflict)]
    public void Exact_runtime_projection_matches_the_guarded_value_or_failure(ResultStatus status)
    {
        // Arrange
        var input = status switch
        {
            ResultStatus.Ok => Result.Ok(2),
            ResultStatus.Created => Result.Created(2),
            ResultStatus.Accepted => Result.Accepted(2),
            ResultStatus.Invalid => Result.Invalid<int>("invalid", "field", "required"),
            _ => Result.Conflict<int>("conflict"),
        };
        var expected = input.IsFailure
            ? input.ConvertError<int, long>()
            : Result.Ok(input.Value + 1L);

        // Act
        var mapped = input.Map(static value => value + 1L);

        // Assert
        mapped.ShouldBe(expected);
    }
}
