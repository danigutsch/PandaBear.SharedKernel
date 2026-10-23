extern alias testingcodefixes;

using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers.Tests;

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
