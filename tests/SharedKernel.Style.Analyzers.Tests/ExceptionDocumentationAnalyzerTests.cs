using System.Globalization;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>Tests for the SKSTYLE018 rule that requires documented exceptions on documented members that throw.</summary>
public sealed class ExceptionDocumentationAnalyzerTests
{
    [Theory]
    [InlineData("throw new InvalidOperationException();", "InvalidOperationException")]
    [InlineData("var x = (string?)null ?? throw new ArgumentException(\"a\");", "ArgumentException")]
    [InlineData("ArgumentNullException.ThrowIfNull(this);", "ArgumentNullException")]
    [InlineData("ArgumentOutOfRangeException.ThrowIfNegative(1);", "ArgumentOutOfRangeException")]
    [InlineData("ObjectDisposedException.ThrowIf(false, this);", "ObjectDisposedException")]
    [InlineData("try { Run(); } catch (ArgumentException) { } throw new InvalidOperationException();", "InvalidOperationException")]
    [InlineData("try { throw new InvalidOperationException(); } catch (ArgumentException) { }", "InvalidOperationException")]
    [InlineData("try { Run(); } catch (Exception) { throw new InvalidOperationException(); }", "InvalidOperationException")]
    [InlineData("try { throw new InvalidOperationException(); } catch (Exception e) when (e is ArgumentException) { }", "InvalidOperationException")]
    [InlineData("try { throw new InvalidOperationException(); } catch (Exception e) when (e.Message.Length > 0) { }", "InvalidOperationException")]
    [InlineData("try { throw new InvalidOperationException(); } catch (Exception e) when (e is not InvalidOperationException) { }", "InvalidOperationException")]
    public async Task Undocumented_thrown_exception_reports_skstyle018(string body, string expected)
    {
        // Arrange
        var source = $$"""
            public sealed class Tour
            {
                /// <summary>Runs.</summary>
                public void Run() { {{body}} }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(candidate => candidate.Id == StyleDiagnosticIds.UndocumentedThrownException
            && candidate.GetMessage(CultureInfo.InvariantCulture).Contains(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("throw new InvalidOperationException();", "<exception cref=\"InvalidOperationException\">Always.</exception>")]
    [InlineData("throw new ArgumentNullException(\"a\");", "<exception cref=\"ArgumentException\">Base type.</exception>")]
    [InlineData("throw new ArgumentNullException(\"a\");", "<exception cref=\"System.ArgumentNullException\" />")]
    [InlineData("ArgumentNullException.ThrowIfNull(this);", "<exception cref=\"ArgumentNullException\">Null.</exception>")]
    [InlineData("throw new InvalidOperationException();", "<inheritdoc />")]
    [InlineData("try { throw new InvalidOperationException(); } catch (InvalidOperationException) { }", "<summary>Runs.</summary>")]
    [InlineData("try { throw new InvalidOperationException(); } catch (Exception) { }", "<summary>Runs.</summary>")]
    [InlineData("try { throw new InvalidOperationException(); } catch (Exception e) when (e is IOException or InvalidOperationException) { }", "<summary>Runs.</summary>")]
    [InlineData("try { throw new ArgumentNullException(\"a\"); } catch (Exception e) when (e is ArgumentException) { }", "<summary>Runs.</summary>")]
    [InlineData("try { Run(); } catch (Exception) { throw; }", "<summary>Runs.</summary>")]
    [InlineData("Action a = () => throw new InvalidOperationException(); a();", "<summary>Runs.</summary>")]
    [InlineData("void Local() => throw new InvalidOperationException(); Local();", "<summary>Runs.</summary>")]
    [InlineData("var d = new Tour().ToString();", "<summary>Runs.</summary>")]
    public async Task Documented_or_handled_exception_does_not_report_skstyle018(string body, string documentation)
    {
        // Arrange
        var source = $$"""
            public sealed class Tour
            {
                /// {{documentation}}
                public void Run() { {{body}} }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == StyleDiagnosticIds.UndocumentedThrownException);
    }

    [Fact]
    public async Task Only_missing_exceptions_are_reported_once_each()
    {
        // Arrange
        const string source = """
            public sealed class Tour
            {
                /// <summary>Runs.</summary>
                /// <exception cref="InvalidOperationException">Always.</exception>
                public void Run(object? a)
                {
                    ArgumentNullException.ThrowIfNull(a);
                    ArgumentNullException.ThrowIfNull(a);
                    throw new InvalidOperationException();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.Count(candidate => candidate.Id == StyleDiagnosticIds.UndocumentedThrownException).ShouldBe(1);
    }

    [Theory]
    [InlineData("public sealed class Tour { public void Run() { throw new InvalidOperationException(); } }")]
    [InlineData("public sealed class Tour { /// <summary>Runs.</summary>\n private void Run() { throw new InvalidOperationException(); } }")]
    [InlineData("public class Base { /// <summary>Runs.</summary>\n /// <exception cref=\"InvalidOperationException\" />\n public virtual void Run() { } }\npublic sealed class Tour : Base { /// <summary>Runs.</summary>\n public override void Run() { throw new InvalidOperationException(); } }")]
    [InlineData("public interface IRun { void Run(); }\npublic sealed class Tour : IRun { /// <summary>Runs.</summary>\n void IRun.Run() { throw new InvalidOperationException(); } }")]
    [InlineData("public sealed class Tour { /// <summary>Runs.</summary>\n public string Name { get => throw new InvalidOperationException(); } }")]
    public async Task Out_of_scope_members_do_not_report_skstyle018(string source)
    {
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(candidate => candidate.Id == StyleDiagnosticIds.UndocumentedThrownException);
    }

    [Fact]
    public async Task Undocumented_constructor_exception_reports_skstyle018()
    {
        // Arrange
        const string source = """
            public sealed class Tour
            {
                /// <summary>Creates.</summary>
                public Tour(object? a) { ArgumentNullException.ThrowIfNull(a); }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(candidate => candidate.Id == StyleDiagnosticIds.UndocumentedThrownException);
    }
}
