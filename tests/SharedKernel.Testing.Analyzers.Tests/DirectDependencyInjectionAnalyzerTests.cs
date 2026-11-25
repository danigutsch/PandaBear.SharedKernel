using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static SharedKernel.Testing.Analyzers.Tests.DirectDependencyInjectionTestSource;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Tests direct dependency injection diagnostics and the opt-in test boundary.</summary>
public sealed class DirectDependencyInjectionAnalyzerTests
{
    [Theory]
    [InlineData("[Fact] public void Uses_services() => _ = new ServiceCollection();", "new ServiceCollection()", "ServiceCollection")]
    [InlineData("[TestCase] public void Uses_services() => _ = new Collection();", "new Collection()", "ServiceCollection")]
    [InlineData("[global::Xunit.Theory] public void Uses_services() { ServiceCollection collection = new(); }", "new()", "ServiceCollection")]
    [InlineData("[Fact] public void Uses_services() => services.BuildServiceProvider();", "services.BuildServiceProvider()", "BuildServiceProvider")]
    [InlineData("[Fact] public void Uses_services() => ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);",
        "ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services)", "BuildServiceProvider")]
    [InlineData("[Fact] public void Uses_services() => scopeFactory.CreateScope();", "scopeFactory.CreateScope()", "CreateScope")]
    [InlineData("[Fact] public void Uses_services() => scopeFactory.CreateAsyncScope();", "scopeFactory.CreateAsyncScope()", "CreateAsyncScope")]
    [InlineData("[Fact] public void Uses_services() => provider.CreateScope();", "provider.CreateScope()", "CreateScope")]
    [InlineData("[Fact] public void Uses_services() => provider.CreateAsyncScope();", "provider.CreateAsyncScope()", "CreateAsyncScope")]
    [InlineData("[Fact] public void Uses_services() => provider.GetService(typeof(string));", "provider.GetService(typeof(string))", "GetService")]
    [InlineData("[Fact] public void Uses_services() => concreteProvider.GetService(typeof(string));", "concreteProvider.GetService(typeof(string))", "GetService")]
    [InlineData("[Fact] public void Uses_services() => provider.GetService<string>();", "provider.GetService<string>()", "GetService")]
    [InlineData("[Fact] public void Uses_services() => provider.GetRequiredService<string>();", "provider.GetRequiredService<string>()", "GetRequiredService")]
    [InlineData("[Fact] public void Uses_services() => provider.GetServices<string>();", "provider.GetServices<string>()", "GetServices")]
    [InlineData("[Fact] public void Uses_services() => keyedProvider.GetKeyedService(typeof(string), null);",
        "keyedProvider.GetKeyedService(typeof(string), null)", "GetKeyedService")]
    [InlineData("[Fact] public void Uses_services() => provider.GetKeyedService<string>(null);", "provider.GetKeyedService<string>(null)", "GetKeyedService")]
    [InlineData("[Fact] public void Uses_services() => provider.GetRequiredKeyedService<string>(null);",
        "provider.GetRequiredKeyedService<string>(null)", "GetRequiredKeyedService")]
    [InlineData("[Fact] public void Uses_services() => provider.GetKeyedServices<string>(null);", "provider.GetKeyedServices<string>(null)", "GetKeyedServices")]
    [InlineData("[Fact] public void Uses_services() => Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<string>());",
        "provider.GetRequiredService<string>()", "GetRequiredService")]
    [InlineData("[Fact] public void Uses_services() { void Read_services() => provider.GetServices<string>(); Read_services(); }",
        "provider.GetServices<string>()", "GetServices")]
    public async Task Direct_dependency_injection_calls_in_xUnit_methods_report_the_bound_operation(string member, string location, string operation)
    {
        // Arrange
        var compilation = Compile(member);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var errors = compilation.GetDiagnostics(cancellationToken).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new TestingAnalyzer()]).GetAnalyzerDiagnosticsAsync(cancellationToken);

        // Assert
        errors.ShouldBeEmpty();
        var diagnostic = diagnostics.ShouldHaveSingleItem(static diagnostic => diagnostic.Id == DiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain(operation, StringComparison.Ordinal);
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(cancellationToken);
        text.ToString(diagnostic.Location.SourceSpan).ShouldBe(location);
    }

    [Theory]
    [InlineData(null, "TestSource.cs")]
    [InlineData(ReportDiagnostic.Suppress, "TestSource.cs")]
    [InlineData(ReportDiagnostic.Warn, "TestSource.g.cs")]
    public async Task Direct_access_rule_respects_default_suppression_and_generated_code(ReportDiagnostic? severity, string path)
    {
        // Arrange
        var compilation = Compile("[Fact] public void Uses_services() => services.BuildServiceProvider();", severity, path);
        var analyzer = new TestingAnalyzer();

        // Act
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        var descriptor = analyzer.SupportedDiagnostics.FirstOrDefault(static descriptor => descriptor.Id == DiagnosticId);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
        descriptor.ShouldNotBeNull().IsEnabledByDefault.ShouldBeFalse();
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.Category.ShouldBe("Testing");
        descriptor.HelpLinkUri.ShouldEndWith("#sktest023", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fixture_operations_and_similarly_named_unrelated_members_are_allowed()
    {
        // Arrange
        var compilation = Compile("""
            public Tests()
            {
                var collection = new Collection();
                _ = collection.BuildServiceProvider();
            }

            public object From_fixture() => provider.GetRequiredService<string>();

            [FixtureFact]
            public void Outside_xUnit() => provider.CreateScope();

            [Fact]
            public void Uses_fixture()
            {
                // new Collection().BuildServiceProvider()
                var text = "provider.GetRequiredService<string>()";
                var item = From_fixture();
                var unrelated = new ServiceCollection();
                unrelated.BuildServiceProvider();
                unrelated.CreateScope();
                unrelated.CreateAsyncScope();
                unrelated.GetService(typeof(string));
                unrelated.GetRequiredService<string>();
            }

            private sealed class FixtureFactAttribute : Attribute;

            private sealed class ServiceCollection
            {
                public void BuildServiceProvider() { }
                public void CreateScope() { }
                public void CreateAsyncScope() { }
                public void GetService(Type type) { }
                public void GetRequiredService<T>() { }
            }
            """);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var errors = compilation.GetDiagnostics(cancellationToken).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new TestingAnalyzer()]).GetAnalyzerDiagnosticsAsync(cancellationToken);

        // Assert
        errors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }
}
