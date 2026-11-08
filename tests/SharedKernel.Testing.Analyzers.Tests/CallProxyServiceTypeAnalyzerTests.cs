using System.Globalization;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST019 rule that CallProxy.Create wraps only a public interface.</summary>
public sealed class CallProxyServiceTypeAnalyzerTests
{
    private const string DiagnosticId = TestingDiagnosticIds.CallProxyServiceType;

    [Theory]
    [InlineData("internal interface IClock { }", "CallProxy.Create<IClock>(null!)", "IClock", "not public")]
    [InlineData("internal interface IClock { }", "CallProxy.Create(new Clock())", "Clock", "not an interface")]
    [InlineData("public class Holder { private interface IClock { } internal static void Wrap() => CallProxy.Create<IClock>(null!); }", "Holder.Wrap()", "IClock", "not public")]
    [InlineData("public class Holder { protected interface IClock { } internal static void Wrap() => CallProxy.Create<IClock>(null!); }", "Holder.Wrap()", "IClock", "not public")]
    [InlineData("internal class Holder { public interface IClock { } }", "CallProxy.Create<Holder.IClock>(null!)", "IClock", "not public")]
    [InlineData("public interface IClock { }", "CallProxy.Create<Clock>(new Clock())", "Clock", "not an interface")]
    [InlineData("public abstract class Base { }", "CallProxy.Create<Base>(null!)", "Base", "not an interface")]
    [InlineData("public interface IClock { }", "PandaBear.Testing.CallProxy.Create<Clock>(new Clock())", "Clock", "not an interface")]
    public async Task Unsupported_service_type_reports_call_proxy_diagnostic(string declarations, string call, string typeName, string reason)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing;

            namespace Demo;

            {{declarations}}

            public class Clock { }

            public sealed class Sample
            {
                public void Wrap()
                {
                    {{call}};
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .ShouldBe($"CallProxy can only wrap a public interface; '{typeName}' is {reason}");
    }

    [Fact]
    public async Task Explicit_type_argument_is_the_reported_location()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            internal interface IClock { }

            public sealed class Sample
            {
                internal static void Wrap(IClock clock) => CallProxy.Create<IClock>(clock);
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe("IClock");
    }

    [Fact]
    public async Task Inferred_type_argument_is_reported_at_the_invocation()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            public class Clock { }

            public sealed class Sample
            {
                public void Wrap() => CallProxy.Create(new Clock());
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe("CallProxy.Create(new Clock())");
    }

    [Theory]
    [InlineData("public interface IClock { }", "IClock", "CallProxy.Create<IClock>(new Clock())")]
    [InlineData("public interface IClock { }", "IClock", "CallProxy.Create((IClock)new Clock())")]
    [InlineData("public interface IClock { }", "IClock", "CallProxy.Create<IClock>(new Clock(), hooks => { })")]
    [InlineData("public class Holder { public interface IClock { } }", "object", "CallProxy.Create<Holder.IClock>(null!)")]
    public async Task Public_interface_is_not_reported(string declarations, string clockBase, string call)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing;

            namespace Demo;

            {{declarations}}

            public class Clock : {{clockBase}} { }

            public sealed class Sample
            {
                public void Wrap()
                {
                    {{call}};
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Compilation_without_shared_kernel_testing_is_inert()
    {
        // Arrange
        const string source = """
            namespace Demo;

            internal interface IClock { }

            public sealed class Sample
            {
                public void Wrap() => Create<IClock>(null!);

                private static T Create<T>(T value) where T : class => value;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, referenceSharedKernelTesting: false);

        // Assert
        diagnostics.Where(static diagnostic => diagnostic.Id == DiagnosticId).ShouldBeEmpty();
    }
}
