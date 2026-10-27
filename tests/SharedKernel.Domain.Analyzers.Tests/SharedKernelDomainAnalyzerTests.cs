using System.Globalization;

namespace PandaBear.Domain.Analyzers.Tests;

/// <summary>Tests that the domain analyzer reports non-private setters (SKDOM001) and exposed mutable collections (SKDOM002) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DomainEncapsulationCapability)]
public sealed class SharedKernelDomainAnalyzerTests
{
    [Fact]
    public void Supported_diagnostics_match_domain_descriptor_contract()
    {
        // Arrange
        var analyzer = new SharedKernelDomainAnalyzer();

        // Act
        var diagnosticIds = analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).ToArray();

        // Assert
        diagnosticIds.ShouldBe([DomainDiagnosticIds.NonPrivateSetter, DomainDiagnosticIds.MutableCollectionExposure]);
    }

    [Theory]
    [InlineData("public string Name { get; set; }")]
    [InlineData("public string Name { get; internal set; }")]
    [InlineData("public string Name { get; protected set; }")]
    [InlineData("public string Name { get; protected internal set; }")]
    [InlineData("public string Name { get; private protected set; }")]
    [InlineData("internal string Name { get; set; }")]
    [InlineData("public static string Name { get; set; }")]
    public async Task Non_private_setter_reports_skdom001(string propertyDeclaration)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public class Tour
            {
                {{propertyDeclaration}}
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DomainDiagnosticIds.NonPrivateSetter);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'Name'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostic_points_at_the_setter_skdom001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        var reportedText = sourceText.ToString(diagnostic.Location.SourceSpan);
        reportedText.ShouldBe("set", "The fix edits the setter, so the diagnostic should point at it.");
    }

    [Theory]
    [InlineData("public string Name { get; private set; } = string.Empty;")]
    [InlineData("public string Name { get; init; } = string.Empty;")]
    [InlineData("public string Name { get; private init; } = string.Empty;")]
    [InlineData("public string Name { get; } = string.Empty;")]
    [InlineData("public string Name => string.Empty;")]
    [InlineData("private string Name { get; set; } = string.Empty;")]
    public async Task Encapsulated_property_does_not_report_skdom001(string propertyDeclaration)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Tour
            {
                {{propertyDeclaration}}
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Positional_record_does_not_report_skdom001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record Money(decimal Amount, string Currency);
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Explicit_interface_setter_reports_skdom001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public interface INamed
            {
                string Name { get; set; }
            }

            public sealed class Tour : INamed
            {
                string INamed.Name { get; set; } = string.Empty;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.Length.ShouldBe(2, "Both the interface contract and its explicit implementation expose the setter.");
        diagnostics.ShouldNotContain(
            static diagnostic => diagnostic.Id != DomainDiagnosticIds.NonPrivateSetter,
            "Only the setter rule applies to these declarations.");
    }

    [Fact]
    public async Task Override_does_not_repeat_skdom001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class Entity
            {
                public abstract string Name { get; set; }
            }

            public sealed class Tour : Entity
            {
                public override string Name { get; set; } = string.Empty;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var reportedLine = AnalyzerTestHarness.GetReportedLine(diagnostic);
        reportedLine.ShouldContain("public abstract string Name", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Generated.g.cs")]
    [InlineData("Tour.designer.cs")]
    public async Task Generated_files_do_not_report_domain_diagnostics(string path)
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public string Name { get; set; } = string.Empty;

                public IList<string> Services { get; } = new List<string>();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generated_code_attribute_does_not_report_domain_diagnostics()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [System.CodeDom.Compiler.GeneratedCode("ModelTool", "1.0")]
            public sealed class Tour
            {
                public string Name { get; set; } = string.Empty;

                public IList<string> Services { get; } = new List<string>();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pragma_suppression_hides_domain_diagnostics()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
            #pragma warning disable SKDOM001, SKDOM002
                public string Name { get; set; } = string.Empty;

                public IList<string> Services { get; } = new List<string>();
            #pragma warning restore SKDOM001, SKDOM002
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("IList<string>")]
    [InlineData("ICollection<string>")]
    [InlineData("ISet<string>")]
    [InlineData("HashSet<string>")]
    [InlineData("Collection<string>")]
    [InlineData("ObservableCollection<string>")]
    [InlineData("IDictionary<string, int>")]
    [InlineData("Dictionary<string, int>")]
    [InlineData("ICollection")]
    [InlineData("ArrayList")]
    public async Task Exposed_mutable_collection_reports_skdom002(string collectionType)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Tour
            {
                public {{collectionType}} Services { get; } = null!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DomainDiagnosticIds.MutableCollectionExposure);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'Services'", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("IReadOnlyCollection<string>")]
    [InlineData("IReadOnlyList<string>")]
    [InlineData("IReadOnlyDictionary<string, int>")]
    [InlineData("IEnumerable<string>")]
    [InlineData("ReadOnlyCollection<string>")]
    [InlineData("ReadOnlyObservableCollection<string>")]
    [InlineData("ReadOnlyDictionary<string, int>")]
    [InlineData("ImmutableArray<string>")]
    [InlineData("ImmutableList<string>")]
    [InlineData("FrozenSet<string>")]
    [InlineData("string")]
    public async Task Read_only_collection_does_not_report_skdom002(string collectionType)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Tour
            {
                public {{collectionType}} Services { get; } = default!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("List<string>")]
    [InlineData("string[]")]
    public async Task Collections_owned_by_sdk_rules_do_not_report_skdom002(string collectionType)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Tour
            {
                public {{collectionType}} Services { get; } = null!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("public sealed class Tour { internal IList<string> Services { get; } = null!; }")]
    [InlineData("internal sealed class Tour { public IList<string> Services { get; } = null!; }")]
    [InlineData("public sealed class Tour { private sealed class Snapshot { public IList<string> Services { get; } = null!; } }")]
    [InlineData("public sealed class Tour { private readonly List<string> services = []; public IReadOnlyList<string> Services => services.AsReadOnly(); }")]
    public async Task Collection_not_visible_outside_the_assembly_does_not_report_skdom002(string typeDeclaration)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            {{typeDeclaration}}
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Protected_collection_on_public_type_reports_skdom002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class Entity
            {
                protected IList<string> Services { get; } = new List<string>();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(DomainDiagnosticIds.MutableCollectionExposure);
    }

    [Fact]
    public async Task Override_and_generic_type_parameter_do_not_report_skdom002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class Entity<TItems>
                where TItems : ICollection<string>
            {
                public abstract IList<string> Services { get; }

                public TItems Items { get; } = default!;
            }

            public sealed class Tour : Entity<List<string>>
            {
                public override IList<string> Services { get; } = new List<string>();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var reportedLine = AnalyzerTestHarness.GetReportedLine(diagnostic);
        reportedLine.ShouldContain("public abstract IList<string> Services", StringComparison.Ordinal);
    }
}
