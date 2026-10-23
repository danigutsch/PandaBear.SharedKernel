using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers.Tests;

public sealed class AssertionUserMessageOverloadAnalyzerTests
{
    private const string DiagnosticId = "SKTEST017";
    private const string MissingOverload = "' has no overload that takes 'string userMessage'";

    [Fact]
    public async Task SKTEST017_is_disabled_by_default()
    {
        // Arrange
        var source = AssertionUserMessageOverloadTestSource.Create("public static void ShouldBeReady(this int actual) { }");
        var analyzer = new SharedKernelTestingAnalyzer();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var descriptor = analyzer.SupportedDiagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == DiagnosticId);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
        descriptor.IsEnabledByDefault.ShouldBeFalse();
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.Category.ShouldBe("Testing");
        descriptor.HelpLinkUri.ShouldEndWith("#sktest017", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public static void ShouldBeReady(this int actual) { }", "ShouldBeReady", "(int)")]
    [InlineData("public static T ShouldBeKind<T>(this object? actual) where T : class => (T)actual!;", "ShouldBeKind", "<T>(object?)")]
    [InlineData("public static void ShouldBeAmong<T>(this T actual, params T[] expected) { }", "ShouldBeAmong", "<T>(T, params T[])")]
    [InlineData(
        "public static Task ShouldBeProblem(this string actual, string? detail = null, CancellationToken cancellationToken = default) => Task.CompletedTask;",
        "ShouldBeProblem",
        "(string, string?, CancellationToken)")]
    [InlineData("public static async Task<int> ShouldFinish(this Task<int> actual) => await actual;", "ShouldFinish", "(Task<int>)")]
    public async Task Assertion_without_a_user_message_overload_reports_SKTEST017(string member, string name, string signature)
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(member));
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe("Assertion 'SampleAssertions." + name + signature + MissingOverload);
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        text.ToString(diagnostic.Location.SourceSpan).ShouldBe(name);
    }

    [Theory]
    [InlineData("public static void ShouldBeReady(this bool actual, string? userMessage = null) { }")]
    [InlineData("public static void ShouldEqual(this int actual, int expected, string userMessage) { }")]
    [InlineData("""
        public static void ShouldEqual<T>(this T actual, T expected) { }
        public static void ShouldEqual<TValue>(this TValue actual, TValue expected, string userMessage) { }
        """)]
    [InlineData("""
        public static Task ShouldBeProblem(this string actual, string? detail = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public static Task ShouldBeProblem(this string actual, string? detail, string userMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        """)]
    [InlineData("""
        public static void ShouldBeAmong<T>(this T actual, params T[] expected) { }
        public static void ShouldBeAmong<T>(this T actual, T[] expected, string userMessage) { }
        """)]
    [InlineData("""
        public static void ShouldHavePair(this object actual, (int a, int b) expected) { }
        public static void ShouldHavePair(this object actual, (int x, int y) expected, string userMessage) { }
        """)]
    [InlineData("""
        public static void ShouldMatchShape(this object actual, dynamic expected) { }
        public static void ShouldMatchShape(this object actual, object expected, string userMessage) { }
        """)]
    public async Task Covered_assertions_do_not_report_SKTEST017(string members)
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(members));
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }

    [Theory]
    [InlineData("", "internal static class InternalAssertions { public static void ShouldBeReady(this int actual) { } }")]
    [InlineData("internal static void ShouldBeReady(this int actual) { }")]
    [InlineData("public static void ShouldBeReady(int actual) { }")]
    [InlineData("public static void Should(this int actual) { }")]
    [InlineData("public static void Shoulder(this int actual) { }")]
    [InlineData("public static bool ShouldBeReady(this int actual) => actual > 0;")]
    [InlineData("public static bool? ShouldBeReady(this int actual) => actual > 0;")]
    [InlineData("[Obsolete(\"Use ShouldBeSet.\")] public static void ShouldBeReady(this int actual) { }")]
    [InlineData("""
        [SuppressMessage("Testing", "SKTEST017:Public assertions should have a userMessage overload", Justification = "The sample keeps its shape.")]
        public static void ShouldBeReady(this int actual) { }
        """)]
    [InlineData("public static void ShouldBeReady(this int actual) { }", "", "SampleAssertions.g.cs")]
    public async Task Methods_that_are_not_assertions_do_not_report_SKTEST017(string members, string declarations = "", string path = "TestSource.cs")
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(members, declarations), path: path);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }

    [Fact]
    public async Task Misplaced_or_foreign_overloads_do_not_cover_SKTEST017()
    {
        // Arrange
        var source = AssertionUserMessageOverloadTestSource.Create(
            """
            public static Task ShouldBeProblem(this string actual, int status, CancellationToken cancellationToken) => Task.CompletedTask;
            public static Task ShouldBeProblem(this string actual, int status, CancellationToken cancellationToken, string userMessage) => Task.CompletedTask;
            public static void ShouldBeReady(this int actual) { }
            """,
            """
            public static class OtherAssertions
            {
                public static void ShouldBeReady(this int actual, string userMessage) { }
            }
            """);
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .ShouldBe(
            [
                "Assertion 'SampleAssertions.ShouldBeProblem(string, int, CancellationToken)" + MissingOverload,
                "Assertion 'SampleAssertions.ShouldBeReady(int)" + MissingOverload,
            ]);
    }

    [Fact]
    public async Task Overloads_with_other_parameter_types_do_not_cover_SKTEST017()
    {
        // Arrange
        var source = AssertionUserMessageOverloadTestSource.Create(
            """
            public static void ShouldEqual<T>(this T actual, T? expected) where T : struct { }
            public static void ShouldEqual<T>(this T actual, T expected, string userMessage) { }
            public static void ShouldHaveItem<T>(this IEnumerable<T> actual, T expected) { }
            public static void ShouldHaveItem<T>(this IEnumerable<T> actual, Predicate<T> predicate, string userMessage) { }
            """);
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .ShouldBe(
            [
                "Assertion 'SampleAssertions.ShouldEqual<T>(T, T?)" + MissingOverload,
                "Assertion 'SampleAssertions.ShouldHaveItem<T>(IEnumerable<T>, T)" + MissingOverload,
            ]);
    }

    [Fact]
    public async Task Partial_assertion_reports_SKTEST017_once_at_its_definition()
    {
        // Arrange
        var source = AssertionUserMessageOverloadTestSource.Create(
            string.Empty,
            """
            public static partial class PartialAssertions
            {
                public static partial void ShouldBeReady(this int actual);

                public static partial void ShouldBeReady(this int actual) { }
            }
            """);
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .ShouldBe("Assertion 'PartialAssertions.ShouldBeReady(int)" + MissingOverload + ": a partial method needs a hand-written overload");
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        diagnostic.Location.SourceSpan.Start.ShouldBe(text.ToString().IndexOf("ShouldBeReady", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        """
        public static T ShouldFail<T>(this Action action) where T : Exception => throw new InvalidOperationException();
        public static T ShouldFail<T>(this Action action, string? paramName) where T : ArgumentException => throw new InvalidOperationException();
        """,
        "ShouldFail<T>(Action)",
        "'SampleAssertions.ShouldFail<T>(Action, string?)' already uses that signature")]
    [InlineData(
        "public static T ShouldExist<T>([NotNull] this T? actual) where T : class => actual ?? throw new InvalidOperationException();",
        "ShouldExist<T>(T?)",
        "parameter 'actual' has [NotNull], whose null state does not flow out of a lambda; write the overload by hand")]
    [InlineData(
        """
        public static void ShouldBeReady(this int actual) { }
        internal static void ShouldBeReady(this int actual, string userMessage) { }
        """,
        "ShouldBeReady(int)",
        "'SampleAssertions.ShouldBeReady(int, string)' already uses that signature")]
    [InlineData(
        """
        public static void ShouldBeReady(this int actual) { }
        private static void ShouldBeReady(this int actual, string userMessage) { }
        """,
        "ShouldBeReady(int)",
        "'SampleAssertions.ShouldBeReady(int, string)' already uses that signature")]
    [InlineData(
        """
        public static void ShouldBeReady(this int actual) { }
        public static void ShouldBeReady(int actual, string userMessage) { }
        """,
        "ShouldBeReady(int)",
        "'SampleAssertions.ShouldBeReady(int, string)' already uses that signature")]
    [InlineData(
        """
        public static void ShouldHaveOther(this object actual, (int a, int b) expected) { }
        public static void ShouldHaveOther(this object actual, (int x, int y) expected, string paramName) { }
        """,
        "ShouldHaveOther(object, (int a, int b))",
        "'SampleAssertions.ShouldHaveOther(object, (int x, int y), string)' already uses that signature")]
    [InlineData("public static void ShouldBeSentence(this string userMessage) { }", "ShouldBeSentence(string)", "a parameter already has the name 'userMessage'")]
    [InlineData("public static void ShouldBeTagged(this int actual, int userMessage) { }", "ShouldBeTagged(int, int)", "a parameter already has the name 'userMessage'")]
    [InlineData("public static void ShouldMatch(this int actual, in int expected) { }", "ShouldMatch(int, in int)", "a lambda cannot capture parameter 'expected'")]
    [InlineData("public static void ShouldBeBlank(this ReadOnlySpan<char> actual) { }", "ShouldBeBlank(ReadOnlySpan<char>)", "a lambda cannot capture parameter 'actual'")]
    [InlineData("public static void ShouldBeDefault<T>(this T actual) where T : allows ref struct { }", "ShouldBeDefault<T>(T)", "a lambda cannot capture parameter 'actual'")]
    [InlineData("public static void ShouldMatchArguments(this int actual, __arglist) { }", "ShouldMatchArguments(int, __arglist)", "a lambda cannot forward '__arglist'")]
    [InlineData("public static ValueTask ShouldComplete(this Task actual) => new(actual);", "ShouldComplete(Task)", "the userMessage helper cannot forward the 'ValueTask' result")]
    [InlineData(
        "public static TTask ShouldComplete<TTask>(this TTask actual) where TTask : Task => actual;",
        "ShouldComplete<TTask>(TTask)",
        "the userMessage helper cannot forward the 'TTask' result")]
    [InlineData(
        """
        public interface IAwaitableCheck { System.Runtime.CompilerServices.TaskAwaiter GetAwaiter(); }
        public interface IPendingCheck : IAwaitableCheck { }
        public static IPendingCheck ShouldEventuallyHold(this Task<int> actual, int expected) => throw new InvalidOperationException();
        """,
        "ShouldEventuallyHold(Task<int>, int)",
        "the userMessage helper cannot forward the 'SampleAssertions.IPendingCheck' result")]
    [InlineData(
        "public static IEnumerable<int> ShouldYield(this int actual) { yield return actual; }",
        "ShouldYield(int)",
        "the userMessage helper cannot forward the 'IEnumerable<int> iterator' result")]
    [InlineData(
        "public static IEnumerable<int> ShouldAllBePositive(this IEnumerable<int> actual) => System.Linq.Enumerable.Select(actual, static value => value > 0 ? value : throw new InvalidOperationException());",
        "ShouldAllBePositive(IEnumerable<int>)",
        "the userMessage helper cannot forward the 'IEnumerable<int>' result")]
    [InlineData(
        "public static IAsyncEnumerable<int> ShouldAllBeEven(this IAsyncEnumerable<int> actual) => actual;",
        "ShouldAllBeEven(IAsyncEnumerable<int>)",
        "the userMessage helper cannot forward the 'IAsyncEnumerable<int>' result")]
    [InlineData("public static Func<int> ShouldEventuallyCount(this int actual) => () => actual;", "ShouldEventuallyCount(int)", "the userMessage helper cannot forward the 'Func<int>' result")]
    [InlineData("public static async void ShouldSettle(this Task actual) => await actual;", "ShouldSettle(Task)", "the userMessage helper cannot forward the 'async void' result")]
    [InlineData("public static ref int ShouldPin(this int[] actual) => ref actual[0];", "ShouldPin(int[])", "the userMessage helper cannot forward the 'ref int' result")]
    [InlineData(
        "public static ReadOnlySpan<char> ShouldTrim(this string actual) => actual.AsSpan();",
        "ShouldTrim(string)",
        "the userMessage helper cannot forward the 'ReadOnlySpan<char>' result")]
    [InlineData(
        "public static T ShouldConvert<T>(this int actual) where T : allows ref struct => throw new InvalidOperationException();",
        "ShouldConvert<T>(int)",
        "the userMessage helper cannot forward the 'T' result")]
    [InlineData(
        "[DoesNotReturn] public static void ShouldNotBeReached(this string reason) => throw new InvalidOperationException(reason);",
        "ShouldNotBeReached(string)",
        "it has [DoesNotReturn], which an expression-bodied forwarder cannot keep (CS8763); write the overload by hand")]
    [InlineData(
        "public static T ShouldBeOr<T>(this T actual, [AllowNull] T fallback) => actual;",
        "ShouldBeOr<T>(T, T)",
        "parameter 'fallback' has [AllowNull], so the forwarded call would infer a nullable type argument; write the overload by hand")]
    [InlineData(
        "public static T ShouldBeLike<T>(this T actual, [AllowNull] T expected) where T : class => actual;",
        "ShouldBeLike<T>(T, T)",
        "parameter 'expected' has [AllowNull], so the forwarded call would infer a nullable type argument; write the overload by hand")]
    [InlineData(
        "public static void ShouldMatchEvent<T>(this T? @event, [AllowNull] T @class) where T : notnull { }",
        "ShouldMatchEvent<T>(T?, T)",
        "parameter 'class' has [AllowNull], so the forwarded call would infer a nullable type argument; write the overload by hand")]
    [InlineData(
        """
        [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Tests compare relative URL text.")]
        public static void ShouldLinkTo(this string actual, string expectedUrl) { }
        """,
        "ShouldLinkTo(string, string)",
        "its suppression of 'CA1054' would not carry over to a generated overload; write the overload by hand with its own justification")]
    [InlineData(
        """
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access", Justification = "The sample keeps its shape.")]
        public static void ShouldSerialize(this object actual) { }
        """,
        "ShouldSerialize(object)",
        "its suppression of 'IL2026' would not carry over to a generated overload; write the overload by hand with its own justification")]
    [InlineData(
        """
        public static void ShouldInclude(this string? actual, string expected) { }
        public static void ShouldInclude(this string? actual, string expected, StringComparison comparisonType) { }
        """,
        "ShouldInclude(string?, string)",
        "forwarding to it would omit the StringComparison that CA1307 requires; forward to 'SampleAssertions.ShouldInclude(string?, string, StringComparison)' by hand")]
    [InlineData(
        """
        public static void ShouldFormatAs(this decimal actual, string expected) { }
        public static void ShouldFormatAs(this decimal actual, string expected, IFormatProvider provider) { }
        """,
        "ShouldFormatAs(decimal, string)",
        "forwarding to it would omit the IFormatProvider that CA1305 requires; forward to 'SampleAssertions.ShouldFormatAs(decimal, string, IFormatProvider)' by hand")]
    [InlineData(
        """
        public static void ShouldLowerTo(this string actual, string expected) { }
        public static void ShouldLowerTo(this string actual, string expected, System.Globalization.CultureInfo culture) { }
        """,
        "ShouldLowerTo(string, string)",
        "forwarding to it would omit the CultureInfo that CA1304 requires; forward to 'SampleAssertions.ShouldLowerTo(string, string, CultureInfo)' by hand")]
    [InlineData(
        "public static void ShouldBeNear(this double actual, double expected, int precision = 2) { }",
        "ShouldBeNear(double, double, int)",
        "its optional parameters would become required in the overload, which RS0027 reports on the original")]
    [InlineData(
        """
        public static void ShouldMatchCount(this int actual, int expected) { }
        public static void ShouldMatchCount(this int actual, int expected, int tolerance = 0) { }
        """,
        "ShouldMatchCount(int, int)",
        "'SampleAssertions.ShouldMatchCount(int, int, int)' would no longer have the most parameters among its public overloads, which RS0027 reports")]
    [InlineData(
        """
        public static Task ShouldEventually(this Task actual, int timeoutMilliseconds) => actual;
        public static Task ShouldEventually(this Task actual, int timeoutMilliseconds, CancellationToken cancellationToken = default) => actual;
        """,
        "ShouldEventually(Task, int)",
        "'SampleAssertions.ShouldEventually(Task, int, CancellationToken)' would no longer have the most parameters among its public overloads, which RS0027 reports")]
    [InlineData(
        """
        public static void ShouldBeKind<T>(this int actual) { }
        public static void ShouldBeKind(this int actual, int expected = 0) { }
        """,
        "ShouldBeKind<T>(int)",
        "'SampleAssertions.ShouldBeKind(int, int)' would no longer have the most parameters among its public overloads, which RS0027 reports")]
    [InlineData(
        """
        public static void ShouldContain<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> actual, TKey key) { }
        public static void ShouldContain<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> actual, TKey key, TValue value) { }
        """,
        "ShouldContain<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>, TKey)",
        "existing calls to 'SampleAssertions.ShouldContain<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>, TKey, TValue)' that pass a string would bind to the overload")]
    [InlineData(
        """
        public static void ShouldBeEither<T>(this T actual, T first, T second) { }
        public static void ShouldBeEither<T>(this T actual, T first, T second, T third) { }
        """,
        "ShouldBeEither<T>(T, T, T)",
        "existing calls to 'SampleAssertions.ShouldBeEither<T>(T, T, T, T)' that pass a string would bind to the overload")]
    [InlineData(
        """
        public static void ShouldHaveHeader(this string actual, string name) { }
        public static void ShouldHaveHeader<T>(this string actual, string name, T value) { }
        """,
        "ShouldHaveHeader(string, string)",
        "existing calls to 'SampleAssertions.ShouldHaveHeader<T>(string, string, T)' that pass a string would bind to the overload")]
    [InlineData(
        "public static void ShouldContainItems(this IEnumerable<object?> actual, params object?[] expected) { }",
        "ShouldContainItems(IEnumerable<object?>, params object?[])",
        "existing calls that pass a string as the last of its 'params' values would bind to the overload")]
    public async Task Blocked_assertions_report_SKTEST017_with_the_reason(string members, string signature, string reason)
    {
        // Arrange
        var expectedMessage = "Assertion 'SampleAssertions." + signature + MissingOverload + ": " + reason;
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(members));
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldHaveSingleItem(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture) == expectedMessage);
    }
}
