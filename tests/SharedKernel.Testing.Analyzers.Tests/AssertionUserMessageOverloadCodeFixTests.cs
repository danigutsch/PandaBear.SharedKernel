extern alias testingcodefixes;

using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the code fix that adds a forwarding userMessage overload for SKTEST017.</summary>
public sealed class AssertionUserMessageOverloadCodeFixTests
{
    private const string DiagnosticId = "SKTEST017";

    [Theory]
    [InlineData(
        """
        /// <summary>Verifies that the value is ready.</summary>
        /// <param name="actual">The actual value.</param>
        public static void ShouldBeReady(this int actual)
        {
        }
        """,
        "ShouldBeReady(this",
        "public static void ShouldBeReady(this int actual, string userMessage) =>",
        "WithUserMessage(userMessage, () => actual.ShouldBeReady());")]
    [InlineData(
        """
        /// <summary>Verifies that the value equals the expected value.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="actual">The actual value.</param>
        /// <param name="expected">The expected value.</param>
        public static void ShouldEqual<T>(this T actual, T expected)
        {
        }
        """,
        "ShouldEqual<T>(this",
        "public static void ShouldEqual<T>(this T actual, T expected, string userMessage) =>",
        "WithUserMessage(userMessage, () => actual.ShouldEqual(expected));")]
    [InlineData(
        """
        /// <summary>Verifies that the value has the kind.</summary>
        /// <typeparam name="T">The expected kind.</typeparam>
        /// <param name="actual">The actual value.</param>
        /// <returns>The value as the kind.</returns>
        public static T ShouldBeKind<T>(this object? actual)
            where T : class => (T)actual!;
        """,
        "ShouldBeKind<T>(this",
        "public static T ShouldBeKind<T>(this object? actual, string userMessage)",
        "WithUserMessage(userMessage, () => actual.ShouldBeKind<T>());")]
    [InlineData(
        """
        /// <summary>Verifies that the value is one of the expected values.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="actual">The actual value.</param>
        /// <param name="expected">The expected values.</param>
        public static void ShouldBeAmong<T>(this T actual, params T[] expected)
        {
        }
        """,
        "ShouldBeAmong<T>(this",
        "public static void ShouldBeAmong<T>(this T actual, T[] expected, string userMessage) =>",
        "WithUserMessage(userMessage, () => actual.ShouldBeAmong(expected));")]
    [InlineData(
        """
        /// <summary>Verifies the problem.</summary>
        /// <param name="actual">The actual response.</param>
        /// <param name="detail">The expected detail, or null to leave it unchecked.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that completes when the problem is verified.</returns>
        public static Task ShouldBeProblem(this string actual, string? detail = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        """,
        "ShouldBeProblem(this",
        "public static Task ShouldBeProblem(this string actual, string? detail, string userMessage, CancellationToken cancellationToken = default) =>",
        "WithUserMessage(userMessage, () => actual.ShouldBeProblem(detail, cancellationToken));")]
    [InlineData(
        """
        /// <summary>Verifies that the task finishes.</summary>
        /// <param name="actual">The actual task.</param>
        /// <returns>The result of the task.</returns>
        public static async Task<int> ShouldFinish(this Task<int> actual) => await actual.ConfigureAwait(false);
        """,
        "ShouldFinish(this",
        "public static Task<int> ShouldFinish(this Task<int> actual, string userMessage) =>",
        "WithUserMessage(userMessage, () => actual.ShouldFinish());")]
    [InlineData(
        """
        /// <summary>Verifies identity semantics.</summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="first">The first entity.</param>
        /// <param name="second">The second entity.</param>
        public static void ShouldHaveIdentity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this T first, T second)
            where T : class
        {
        }
        """,
        "ShouldHaveIdentity<",
        "public static void ShouldHaveIdentity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this T first, T second, string userMessage)",
        "where T : class =>",
        "WithUserMessage(userMessage, () => first.ShouldHaveIdentity(second));")]
    public async Task Fix_adds_a_forwarding_overload_after_the_assertion(string members, string marker, params string[] fragments)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(fragments);
        var workspace = CodeFixTestWorkspace.Create(
            AssertionUserMessageOverloadCodeFixTestSource.Create(members),
            includeDefaultUsings: false,
            documentationMode: DocumentationMode.Diagnose);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        var action = actions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var updated = await workspace.GetDocumentText();
        var compilation = await workspace.Workspace.CurrentSolution.Projects.ShouldHaveSingleItem().GetCompilationAsync(TestContext.Current.CancellationToken);
        var analyzerDiagnostics = await workspace.GetAnalyzerDiagnostics(new SharedKernelTestingAnalyzer(), DiagnosticId);

        // Assert
        action.Title.ShouldBe("Add userMessage overload");
        action.EquivalenceKey.ShouldBe("AddUserMessageOverload");
        fragments.ShouldAllSatisfy(fragment => updated.ShouldContain(fragment, StringComparison.Ordinal));
        updated.IndexOf(fragments[0], StringComparison.Ordinal).ShouldBeGreaterThan(updated.IndexOf(marker, StringComparison.Ordinal));
        compilation.ShouldNotBeNull().GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static compilerDiagnostic => compilerDiagnostic.Severity >= DiagnosticSeverity.Warning)
            .ShouldBeEmpty();
        analyzerDiagnostics.ShouldNotContain(static analyzerDiagnostic => analyzerDiagnostic.Id == DiagnosticId);
    }

    [Fact]
    public async Task Fix_writes_the_expression_bodied_shape()
    {
        // Arrange
        const string expected = """
                    where T : class => (T)actual!;

                /// <summary>Verifies that the value has the kind.</summary>
                /// <typeparam name="T">The expected kind.</typeparam>
                /// <param name="actual">The actual value.</param>
                /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
                /// <returns>The value as the kind.</returns>
                /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
                public static T ShouldBeKind<T>(this object? actual, string userMessage)
                    where T : class =>
                    WithUserMessage(userMessage, () => actual.ShouldBeKind<T>());
            }
            """;
        var source = AssertionUserMessageOverloadCodeFixTestSource.Create(
            """
            /// <summary>Verifies that the value has the kind.</summary>
            /// <typeparam name="T">The expected kind.</typeparam>
            /// <param name="actual">The actual value.</param>
            /// <returns>The value as the kind.</returns>
            public static T ShouldBeKind<T>(this object? actual)
                where T : class => (T)actual!;
            """);
        var workspace = CodeFixTestWorkspace.Create(source, includeDefaultUsings: false);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldBeKind<T>(this");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ReplaceLineEndings("\n").ShouldContain(expected.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_omits_type_arguments_that_inference_supplies()
    {
        // Arrange
        var source = AssertionUserMessageOverloadCodeFixTestSource.Create(
            """
            /// <summary>Verifies that the value is ready.</summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="actual">The actual value.</param>
            /// <param name="expected">The expected value.</param>
            public static void ShouldBeReady<T>(this T actual, T expected)
            {
            }

            /// <summary>Verifies that the value has the kind.</summary>
            /// <typeparam name="T">The expected kind.</typeparam>
            /// <param name="actual">The actual value.</param>
            /// <returns>The value as the kind.</returns>
            public static T ShouldBeKind<T>(this object? actual)
                where T : class => (T)actual!;
            """);
        var workspace = CodeFixTestWorkspace.Create(source, includeDefaultUsings: false);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var readyActions = await workspace.GetCodeActions(provider, await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldBeReady<T>(this"));
        await workspace.ApplyCodeAction(readyActions.ShouldHaveSingleItem());
        var kindActions = await workspace.GetCodeActions(provider, await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldBeKind<T>(this"));
        await workspace.ApplyCodeAction(kindActions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ShouldContain("actual.ShouldBeReady(expected)", StringComparison.Ordinal);
        updated.ShouldNotContain("actual.ShouldBeReady<T>(expected)", StringComparison.Ordinal);
        updated.ShouldContain("actual.ShouldBeKind<T>()", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_documents_the_user_message_after_the_preceding_parameter()
    {
        // Arrange
        var source = AssertionUserMessageOverloadCodeFixTestSource.Create(
            """
            /// <summary>Verifies the problem.</summary>
            /// <param name="actual">The actual response.</param>
            /// <param name="detail">The expected detail, or null to leave it unchecked.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A task that completes when the problem is verified.</returns>
            public static Task ShouldBeProblem(this string actual, string? detail = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            """).ReplaceLineEndings("\r\n");
        var workspace = CodeFixTestWorkspace.Create(source, includeDefaultUsings: false, documentationMode: DocumentationMode.Diagnose);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldBeProblem(this");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ShouldContain(
            "    /// <param name=\"detail\">The expected detail, or null to leave it unchecked.</param>\r\n"
            + "    /// <param name=\"userMessage\">Why the test expects the assertion to hold; the failure message starts with it.</param>\r\n"
            + "    /// <param name=\"cancellationToken\">The cancellation token.</param>\r\n",
            StringComparison.Ordinal);
        updated.ShouldContain(
            "    /// <returns>A task that completes when the problem is verified.</returns>\r\n"
            + "    /// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"userMessage\"/> is null.</exception>\r\n"
            + "    public static Task ShouldBeProblem(this string actual, string? detail, string userMessage, CancellationToken cancellationToken = default) =>\r\n",
            StringComparison.Ordinal);
        updated.ReplaceLineEndings("\r\n").ShouldBe(updated);
    }

    [Theory]
    [InlineData(
        """
        /// <summary>Verifies that the value is ready.</summary>
        public static void ShouldBeReady(this int actual)
        {
        }
        """,
        "ShouldBeReady(this")]
    [InlineData(
        """
        /// <summary>Verifies that the value has the kind.</summary>
        /// <typeparam name="T">The expected kind.</typeparam>
        /// <returns>The value as the kind.</returns>
        public static T ShouldBeKind<T>(this object? actual)
            where T : class => (T)actual!;
        """,
        "ShouldBeKind<T>(this")]
    [InlineData(
        """
        /// <summary>Verifies that the value equals the expected value.</summary>
        /// <param name="actual">The actual value.</param>
        /// <param name="expected">The expected value.</param>
        public static void ShouldEqual(this int actual, int expected)
        {
        }

        /// <inheritdoc cref="ShouldEqual(int, int)"/>
        public static void ShouldMatch(this int actual, int expected)
        {
        }
        """,
        "ShouldMatch(this")]
    public async Task Fix_documents_only_the_exception_when_the_assertion_documents_no_parameters(string members, string marker)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(members);
        var workspace = CodeFixTestWorkspace.Create(
            AssertionUserMessageOverloadCodeFixTestSource.Create(members),
            includeDefaultUsings: false,
            documentationMode: DocumentationMode.Diagnose);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();
        var compilation = await workspace.Workspace.CurrentSolution.Projects.ShouldHaveSingleItem().GetCompilationAsync(TestContext.Current.CancellationToken);

        // Assert
        updated.ShouldContain(
            "/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"userMessage\"/> is null.</exception>",
            StringComparison.Ordinal);
        updated.ShouldNotContain("<param name=\"userMessage\">", StringComparison.Ordinal);
        compilation.ShouldNotBeNull().GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static compilerDiagnostic => compilerDiagnostic.Severity >= DiagnosticSeverity.Warning)
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData(
        """
        /// <summary>Verifies that the values include the expected value.</summary>
        /// <param name="actual">The actual values.</param>
        /// <param name="expected">The expected value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null.</exception>
        public static void ShouldInclude(this string[] actual, string expected) => ArgumentNullException.ThrowIfNull(expected);
        """,
        """
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
            public static void ShouldInclude(this string[] actual, string expected, string userMessage) =>
        """)]
    [InlineData(
        """
        /// <summary>Verifies that the values include the expected value.</summary>
        /// <param name="actual">The actual values.</param>
        /// <param name="expected">The expected value.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actual"/> or <paramref name="expected"/> is null.</exception>
        public static void ShouldInclude(this string[] actual, string expected) => ArgumentNullException.ThrowIfNull(expected);
        """,
        """
            /// <exception cref="System.ArgumentNullException"><paramref name="actual"/> or <paramref name="expected"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
            public static void ShouldInclude(this string[] actual, string expected, string userMessage) =>
        """)]
    [InlineData(
        """
        /// <summary>Verifies that the values include the expected value.</summary>
        /// <param name="actual">The actual values.</param>
        /// <param name="expected">The expected value.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="expected"/> is null.
        /// </exception>
        public static void ShouldInclude(this string[] actual, string expected) => ArgumentNullException.ThrowIfNull(expected);
        """,
        """
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="expected"/> is null.
            /// Also thrown when <paramref name="userMessage"/> is null.
            /// </exception>
            public static void ShouldInclude(this string[] actual, string expected, string userMessage) =>
        """)]
    public async Task Fix_adds_the_user_message_to_an_existing_argument_null_exception_tag(string members, string expected)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(expected);
        var workspace = CodeFixTestWorkspace.Create(
            AssertionUserMessageOverloadCodeFixTestSource.Create(members),
            includeDefaultUsings: false,
            documentationMode: DocumentationMode.Diagnose);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldInclude(this");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = (await workspace.GetDocumentText()).ReplaceLineEndings("\n");

        // Assert
        updated.ShouldContain(expected.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        updated.Split("<exception cref=", StringSplitOptions.None).Length.ShouldBe(3);
    }

    [Theory]
    [InlineData(
        """
        public static void ShouldBeNear(this int actual, // receiver
            int expected) // expected
        {
        }
        """,
        "ShouldBeNear(this",
        """
            public static void ShouldBeNear(this int actual, // receiver
                int expected,
                string userMessage) =>
                WithUserMessage(userMessage, () => actual.ShouldBeNear(expected));
        """)]
    [InlineData(
        """
        public static void ShouldHaveStatus(this string actual,
            int expected, // HTTP status code
            CancellationToken cancellationToken = default)
        {
        }
        """,
        "ShouldHaveStatus(this",
        """
            public static void ShouldHaveStatus(this string actual,
                int expected, // HTTP status code
                string userMessage,
                CancellationToken cancellationToken = default) =>
                WithUserMessage(userMessage, () => actual.ShouldHaveStatus(expected, cancellationToken));
        """)]
    [InlineData(
        """
        public static void ShouldBeSpread<T>(this T actual, // the actual value
            /* the expected value */ T expected)
            where T : class // reference types only
        {
        }
        """,
        "ShouldBeSpread<T>(this",
        """
            public static void ShouldBeSpread<T>(this T actual, // the actual value
                /* the expected value */ T expected,
                string userMessage)
                where T : class =>
                WithUserMessage(userMessage, () => actual.ShouldBeSpread(expected));
        """)]
    [InlineData(
        """
        public static void ShouldBePositive(
            this int actual
        )
        {
        }
        """,
        "ShouldBePositive(",
        """
            public static void ShouldBePositive(
                this int actual,
                string userMessage
            ) =>
                WithUserMessage(userMessage, () => actual.ShouldBePositive());
        """)]
    [InlineData(
        """
        public static void ShouldBeAbove(
            this int actual,
            int expected
        )
        {
        }
        """,
        "ShouldBeAbove(",
        """
            public static void ShouldBeAbove(
                this int actual,
                int expected,
                string userMessage
            ) =>
                WithUserMessage(userMessage, () => actual.ShouldBeAbove(expected));
        """)]
    [InlineData(
        """
        public static void ShouldBeBelow(
            this int actual,
            int expected // the bound
        )
        {
        }
        """,
        "ShouldBeBelow(",
        """
            public static void ShouldBeBelow(
                this int actual,
                int expected, // the bound
                string userMessage
            ) =>
                WithUserMessage(userMessage, () => actual.ShouldBeBelow(expected));
        """)]
    public async Task Fix_keeps_the_comments_and_line_breaks_of_the_parameter_list(string members, string marker, string expectedOverload)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(expectedOverload);
        var source = AssertionUserMessageOverloadCodeFixTestSource.Create(members).ReplaceLineEndings("\n");
        var expectedStart = source[..source.IndexOf("\n}\n", StringComparison.Ordinal)] + "\n\n" + expectedOverload.ReplaceLineEndings("\n") + "\n}\n";
        var workspace = CodeFixTestWorkspace.Create(source, includeDefaultUsings: false);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = (await workspace.GetDocumentText()).ReplaceLineEndings("\n");

        // Assert
        updated.ShouldStartWith(expectedStart, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        """
        public static T ShouldFail<T>(this Action action) where T : Exception => throw new InvalidOperationException();
        public static T ShouldFail<T>(this Action action, string? paramName) where T : ArgumentException => throw new InvalidOperationException();
        """,
        "ShouldFail<T>(this")]
    [InlineData("public static T ShouldExist<T>([NotNull] this T? actual) where T : class => actual ?? throw new InvalidOperationException();", "ShouldExist<T>(")]
    [InlineData("public static void ShouldMatch(this int actual, in int expected) { }", "ShouldMatch(this")]
    [InlineData("public static void ShouldBeBlank(this ReadOnlySpan<char> actual) { }", "ShouldBeBlank(this")]
    [InlineData("public static ValueTask ShouldComplete(this Task actual) => new(actual);", "ShouldComplete(this")]
    [InlineData("public static IEnumerable<int> ShouldYield(this int actual) { yield return actual; }", "ShouldYield(this")]
    [InlineData("public static async void ShouldSettle(this Task actual) => await actual.ConfigureAwait(false);", "ShouldSettle(this")]
    [InlineData("public static ref int ShouldPin(this int[] actual) => ref actual[0];", "ShouldPin(this")]
    [InlineData(
        """
        public static void ShouldInclude(this string? actual, string expected) { }
        public static void ShouldInclude(this string? actual, string expected, StringComparison comparisonType) { }
        """,
        "ShouldInclude(this")]
    [InlineData("public static void ShouldBeNear(this double actual, double expected, int precision = 2) { }", "ShouldBeNear(this")]
    [InlineData(
        """
        public static void ShouldMatchCount(this int actual, int expected) { }
        public static void ShouldMatchCount(this int actual, int expected, int tolerance = 0) { }
        """,
        "ShouldMatchCount(this")]
    [InlineData(
        """
        public static Task ShouldEventually(this Task actual, int timeoutMilliseconds) => actual;
        public static Task ShouldEventually(this Task actual, int timeoutMilliseconds, CancellationToken cancellationToken = default) => actual;
        """,
        "ShouldEventually(this")]
    [InlineData(
        """
        public static void ShouldBeKind<T>(this int actual) { }
        public static void ShouldBeKind(this int actual, int expected = 0) { }
        """,
        "ShouldBeKind<T>(this")]
    [InlineData(
        """
        public static void ShouldFormatAs(this decimal actual, string expected) { }
        public static void ShouldFormatAs(this decimal actual, string expected, IFormatProvider provider) { }
        """,
        "ShouldFormatAs(this")]
    [InlineData(
        """
        public static void ShouldLowerTo(this string actual, string expected) { }
        public static void ShouldLowerTo(this string actual, string expected, System.Globalization.CultureInfo culture) { }
        """,
        "ShouldLowerTo(this")]
    [InlineData(
        """
        [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Tests compare relative URL text.")]
        public static void ShouldLinkTo(this string actual, string expectedUrl) { }
        """,
        "ShouldLinkTo(this")]
    [InlineData(
        """
        [UnconditionalSuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Tests compare relative URL text.")]
        public static void ShouldLinkTo(this string actual, string expectedUrl) { }
        """,
        "ShouldLinkTo(this")]
    [InlineData(
        """
        public static void ShouldContain<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> actual, TKey key) { }
        public static void ShouldContain<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> actual, TKey key, TValue value) { }
        """,
        "ShouldContain<TKey, TValue>(this")]
    [InlineData(
        """
        public static void ShouldBeEither<T>(this T actual, T first, T second) { }
        public static void ShouldBeEither<T>(this T actual, T first, T second, T third) { }
        """,
        "ShouldBeEither<T>(this")]
    [InlineData("public static void ShouldContainItems(this IEnumerable<object?> actual, params object?[] expected) { }", "ShouldContainItems(this")]
    [InlineData("public static TTask ShouldComplete<TTask>(this TTask actual) where TTask : Task => actual;", "ShouldComplete<TTask>(this")]
    [InlineData(
        """
        public struct EventualCheck
        {
        }

        public static EventualCheck ShouldEventuallyHold(this int actual) => default;

        public static System.Runtime.CompilerServices.TaskAwaiter GetAwaiter(this EventualCheck check) => Task.CompletedTask.GetAwaiter();
        """,
        "ShouldEventuallyHold(this")]
    [InlineData(
        "public static IEnumerable<int> ShouldAllBePositive(this IEnumerable<int> actual) => System.Linq.Enumerable.Select(actual, static value => value > 0 ? value : throw new InvalidOperationException());",
        "ShouldAllBePositive(this")]
    [InlineData("public static void ShouldBeDefault<T>(this T actual) where T : allows ref struct { }", "ShouldBeDefault<T>(this")]
    [InlineData("public static T ShouldConvert<T>(this int actual) where T : allows ref struct => throw new InvalidOperationException();", "ShouldConvert<T>(this")]
    [InlineData("[DoesNotReturn] public static void ShouldNotBeReached(this string reason) => throw new InvalidOperationException(reason);", "ShouldNotBeReached(this")]
    [InlineData("public static T ShouldBeOr<T>(this T actual, [AllowNull] T fallback) => actual;", "ShouldBeOr<T>(this")]
    [InlineData("public static void ShouldMatchArguments(this int actual, __arglist) { }", "ShouldMatchArguments(this")]
    public async Task Fix_is_not_offered_for_blocked_assertions(string members, string marker)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(members);
        var workspace = CodeFixTestWorkspace.Create(AssertionUserMessageOverloadCodeFixTestSource.Create(members), includeDefaultUsings: false);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(
        """
        /// <summary>Verifies that the value is ready.</summary>
        /// <param name="actual">The actual value.</param>
        public static void ShouldBeReady(this int actual)
        {
        }
        """,
        "ShouldBeReady(this",
        "")]
    [InlineData(
        """
        /// <summary>Verifies that the task completes.</summary>
        /// <param name="actual">The actual task.</param>
        /// <returns>The task.</returns>
        public static Task ShouldComplete(this Task actual) => actual;
        """,
        "ShouldComplete(this",
        AssertionUserMessageOverloadCodeFixTestSource.HelperWithoutTaskShapes)]
    public async Task Fix_is_not_offered_without_a_user_message_helper(string members, string marker, string helper)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(helper);
        var workspace = CodeFixTestWorkspace.Create(AssertionUserMessageOverloadCodeFixTestSource.Create(members, helper), includeDefaultUsings: false);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Fix_is_not_offered_when_the_overload_adds_a_compiler_diagnostic()
    {
        // Arrange
        // A null third argument converts to both the comparer and the message, so the call would become CS0121.
        var source = AssertionUserMessageOverloadCodeFixTestSource.Create(
            """
            /// <summary>Verifies that the values contain the item.</summary>
            /// <typeparam name="T">The item type.</typeparam>
            /// <param name="actual">The actual values.</param>
            /// <param name="expected">The expected item.</param>
            public static void ShouldHaveItem<T>(this IEnumerable<T> actual, T expected)
            {
            }

            /// <summary>Verifies that the values contain the item by a comparer.</summary>
            /// <typeparam name="T">The item type.</typeparam>
            /// <param name="actual">The actual values.</param>
            /// <param name="expected">The expected item.</param>
            /// <param name="comparer">The item comparer, or null for the default comparer.</param>
            public static void ShouldHaveItem<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T>? comparer)
            {
            }

            internal static void Check(string[] values, string item) => values.ShouldHaveItem(item, null);
            """);
        var workspace = CodeFixTestWorkspace.Create(source, includeDefaultUsings: false);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldHaveItem<T>(this");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Fix_is_not_offered_for_a_stale_diagnostic()
    {
        // Arrange
        var source = AssertionUserMessageOverloadCodeFixTestSource.Create(
            """
            /// <summary>Verifies that the value is ready.</summary>
            /// <param name="actual">The actual value.</param>
            public static void ShouldBeReady(this int actual)
            {
            }

            /// <summary>Verifies that the value is ready.</summary>
            /// <param name="actual">The actual value.</param>
            /// <param name="userMessage">Why the test expects the value to be ready.</param>
            public static void ShouldBeReady(this int actual, string userMessage) =>
                WithUserMessage(userMessage, () => actual.ShouldBeReady());
            """);
        var workspace = CodeFixTestWorkspace.Create(source, includeDefaultUsings: false);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldBeReady(this");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }
}
