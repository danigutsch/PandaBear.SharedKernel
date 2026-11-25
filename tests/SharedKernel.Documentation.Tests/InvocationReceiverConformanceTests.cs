using Fixture = PandaBear.Documentation.Tests.InvocationReceiverConformanceFixture;

namespace PandaBear.Documentation.Tests;

/// <summary>Verifies receiver-qualified conformance checks against real invocation shapes.</summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class InvocationReceiverConformanceTests
{
    [Fact]
    public void Distinguishes_initializers_with_the_same_name_and_different_receivers()
    {
        // Arrange
        using var workspace = Fixture.Create(Fixture.Initializers);

        // Act
        var checkedFacts = DocumentationConformanceChecker.Check(workspace.RootPath, Fixture.ConfigurationPath);

        // Assert
        checkedFacts.ShouldBe(1);
    }

    [Theory]
    [InlineData("""
        Fixtures.PostgreSqlEventSourcingSchema.Initialize(catalogDataSource, options: null, ct);
        Initialize(catalogDataSource, options: null, ct);
        CatalogGalleryProcessStatusIndexes.Initialize(catalogDataSource, ct);
        """, "found 0")]
    [InlineData("""
        PostgreSqlEventSourcingSchema.Initialize(catalogDataSource, options: null, ct);
        PostgreSqlEventSourcingSchema.Initialize(otherDataSource, options: null, ct);
        CatalogGalleryProcessStatusIndexes.Initialize(catalogDataSource, ct);
        """, "found 2")]
    [InlineData("""
        PostgreSqlEventSourcingSchema.Initialize(catalogDataSource, options: false, ct);
        CatalogGalleryProcessStatusIndexes.Initialize(catalogDataSource, ct);
        """, "unexpected arguments")]
    public void Qualified_checks_keep_the_exact_count_and_argument_requirements(string calls, string expectedDiagnostic)
    {
        // Arrange
        using var workspace = Fixture.Create(calls);
        Action check = () => DocumentationConformanceChecker.Check(workspace.RootPath, Fixture.ConfigurationPath);

        // Act
        var failure = check.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("PostgreSqlEventSourcingSchema.Initialize", StringComparison.Ordinal);
        failure.Message.ShouldContain(expectedDiagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Qualified_counts_ignore_generic_type_arguments_and_unqualified_calls()
    {
        // Arrange
        using var workspace = Fixture.Create(Fixture.Initializers + "\n" + """
            PostgreSqlEventSourcingSchema.Register<Initialize>();
            Initialize(catalogDataSource, ct);
            """);

        // Act
        var checkedFacts = DocumentationConformanceChecker.Check(workspace.RootPath, Fixture.ConfigurationPath);

        // Assert
        checkedFacts.ShouldBe(1);
    }

    [Fact]
    public void Top_level_counts_can_select_an_explicit_receiver()
    {
        // Arrange
        using var workspace = Fixture.Create(Fixture.Initializers, """
            {
              "sourcePath": "src/DatabaseInitializationWorker.cs",
              "invokedMethodName": "Initialize",
              "receiver": "CatalogGalleryProcessStatusIndexes",
              "expectedCount": 1
            }
            """, topLevel: true);

        // Act
        var checkedFacts = DocumentationConformanceChecker.Check(workspace.RootPath, Fixture.ConfigurationPath);

        // Assert
        checkedFacts.ShouldBe(1);
    }

    [Fact]
    public void Omitting_the_receiver_keeps_legacy_identifier_counting()
    {
        // Arrange
        using var workspace = Fixture.Create(Fixture.Initializers + "\nPostgreSqlEventSourcingSchema.Register<Initialize>();", """
            {
              "sourcePath": "src/DatabaseInitializationWorker.cs",
              "methodName": "RunMigrations",
              "parameterCount": 2,
              "invokedMethodName": "Initialize",
              "expectedCount": 3
            }
            """);

        // Act
        var checkedFacts = DocumentationConformanceChecker.Check(workspace.RootPath, Fixture.ConfigurationPath);

        // Assert
        checkedFacts.ShouldBe(1);
    }
}
