using System.Globalization;
using Microsoft.CodeAnalysis;

namespace PandaBear.EntityFrameworkCore.Analyzers.Tests;

/// <summary>Tests that the Entity Framework Core analyzer reports unique indexes without a rule (SKEF001) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.UniqueIndexRuleCapability)]
public sealed class SharedKernelEntityFrameworkCoreAnalyzerTests
{
    [Fact]
    public void Supported_diagnostics_match_entity_framework_core_descriptor_contract()
    {
        // Arrange
        var analyzer = new SharedKernelEntityFrameworkCoreAnalyzer();

        // Act
        var descriptors = analyzer.SupportedDiagnostics;

        // Assert
        var descriptor = descriptors.ShouldHaveSingleItem();
        descriptor.Id.ShouldBe(EntityFrameworkCoreDiagnosticIds.UntaggedUniqueIndex);
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.Category.ShouldBe("Design");
    }

    [Theory]
    [InlineData("builder.HasIndex(x => x.Email).IsUnique();")]
    [InlineData("builder.HasIndex(x => x.Email).IsUnique(true);")]
    [InlineData("builder.HasIndex(\"Email\").IsUnique();")]
    [InlineData("builder.HasIndex(x => x.Email).HasDatabaseName(\"ix\").IsUnique();")]
    public async Task Unique_index_without_rule_reports_skef001_in_on_model_creating(string statement)
    {
        // Arrange
        var source = UniqueIndexSources.InOnModelCreating(statement);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(EntityFrameworkCoreDiagnosticIds.UntaggedUniqueIndex);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'Email'", StringComparison.Ordinal);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("HasUniqueRule", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unique_index_without_rule_reports_skef001_in_entity_type_configuration()
    {
        // Arrange
        var source = UniqueIndexSources.InConfiguration("builder.HasIndex(x => x.Email).IsUnique();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(EntityFrameworkCoreDiagnosticIds.UntaggedUniqueIndex);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain("IsUnique()", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Composite_unique_index_names_every_property()
    {
        // Arrange
        var source = UniqueIndexSources.InConfiguration("builder.HasIndex(x => new { x.Email, x.Name }).IsUnique();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'Email, Name'", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[Index(nameof(Customer.Email), IsUnique = true)]")]
    [InlineData("[Index(\"Email\", \"Name\", IsUnique = true)]")]
    [InlineData("[Microsoft.EntityFrameworkCore.Index(nameof(Customer.Email), IsUnique = true, Name = \"ix\")]")]
    public async Task Unique_index_attribute_reports_skef001_with_fluent_hint(string attribute)
    {
        // Arrange
        var source = UniqueIndexSources.WithClassAttribute(attribute);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(EntityFrameworkCoreDiagnosticIds.UntaggedUniqueIndex);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("fluent API", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("builder.HasIndex(x => x.Email).IsUnique().HasUniqueRule(\"customer.email\");")]
    [InlineData("builder.HasIndex(x => x.Email).HasUniqueRule(\"customer.email\").IsUnique();")]
    [InlineData("builder.HasIndex(x => x.Email).HasUniqueRule(\"customer.email\");")]
    [InlineData("builder.HasIndex(x => x.Email).IsUnique(false);")]
    [InlineData("builder.HasIndex(x => x.Email);")]
    [InlineData("builder.HasIndex(x => x.Email).HasDatabaseName(\"ix\");")]
    [InlineData("builder.HasKey(x => x.Id);")]
    [InlineData("builder.HasAlternateKey(x => x.Email);")]
    [InlineData("var enabled = true; builder.HasIndex(x => x.Email).IsUnique(enabled);")]
    public async Task Tagged_or_non_unique_index_reports_nothing(string statement)
    {
        // Arrange
        var source = UniqueIndexSources.InConfiguration(statement);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("var index = builder.HasIndex(x => x.Email); index.IsUnique(); index.HasUniqueRule(\"customer.email\");")]
    [InlineData("var index = builder.HasIndex(x => x.Email).IsUnique(); index.HasUniqueRule(\"customer.email\");")]
    public async Task Index_variable_tagged_in_the_same_block_reports_nothing(string statements)
    {
        // Arrange
        var source = UniqueIndexSources.InConfiguration(statements);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Index_variable_never_tagged_reports_skef001()
    {
        // Arrange
        var source = UniqueIndexSources.InConfiguration("var index = builder.HasIndex(x => x.Email); index.IsUnique();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(EntityFrameworkCoreDiagnosticIds.UntaggedUniqueIndex);
    }

    [Theory]
    [InlineData("[Index(nameof(Customer.Email))]")]
    [InlineData("[Index(nameof(Customer.Email), IsUnique = false)]")]
    public async Task Non_unique_index_attribute_reports_nothing(string attribute)
    {
        // Arrange
        var source = UniqueIndexSources.WithClassAttribute(attribute);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Compilation_without_shared_kernel_entity_framework_core_reports_nothing()
    {
        // Arrange
        var source = UniqueIndexSources.InConfiguration("builder.HasIndex(x => x.Email).IsUnique();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, withSharedKernel: false);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Compilation_without_shared_kernel_entity_framework_core_ignores_unique_index_attribute()
    {
        // Arrange
        var source = UniqueIndexSources.WithClassAttribute("[Index(nameof(Customer.Email), IsUnique = true)]");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, withSharedKernel: false);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
