using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharedKernel.Results.SourceGenerator;

namespace SharedKernel.Results.GeneratorTests;

public sealed class GeneratorImplementationTests
{
    [Fact]
    public void Catalog_Models_With_The_Same_Entries_Are_Equal()
    {
        // Arrange
        var entry = new ErrorCatalogEntryModel(
            "demo-usererrors-missingname",
            "docs/errors/README.md",
            "Demo.UserErrors",
            "MissingName",
            "Invalid",
            "invalid",
            "Name is required.",
            "Name is required.");
        var first = new ErrorCatalogModel([entry]);
        var second = new ErrorCatalogModel([entry]);

        // Act
        var areEqual = first.Equals(second);

        // Assert
        Assert.True(areEqual);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Catalog_Models_With_Different_Entries_Are_Not_Equal()
    {
        // Arrange
        var firstEntry = new ErrorCatalogEntryModel(
            "demo-usererrors-missingname",
            "docs/errors/README.md",
            "Demo.UserErrors",
            "MissingName",
            "Invalid",
            "invalid",
            "Name is required.",
            "Name is required.");
        var secondEntry = new ErrorCatalogEntryModel(
            "demo-usererrors-savefailed",
            "docs/errors/README.md",
            "Demo.UserErrors",
            "SaveFailed",
            "Error",
            "error",
            "Save failed.",
            null);
        var first = new ErrorCatalogModel([firstEntry]);
        var second = new ErrorCatalogModel([secondEntry]);

        // Act
        var matchesDifferentObject = Equals(first, new object());

        // Assert
        Assert.NotEqual(first, second);
        Assert.False(matchesDifferentObject);
    }

    [Fact]
    public void Build_Discovers_Static_Error_Providers_And_Extracts_Metadata()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public static class UserErrors
            {
                public static Result MissingName() => Result.Invalid(
                    detail: "Name is required.",
                    field: "Name",
                    message: "Name is required.");

                public static Result SaveFailed() => Result.Error("Save failed.");
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.Single();
        var classDeclaration = syntaxTree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();

        // Act
        var entries = ErrorCatalogModelBuilder.Build(
            classDeclaration,
            compilation.GetSemanticModel(syntaxTree),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, entries.Length);

        var invalidEntry = Assert.Single(entries, static entry => entry.MemberName == "MissingName");
        Assert.Equal("demo-usererrors-missingname", invalidEntry.Identifier);
        Assert.Equal("Invalid", invalidEntry.Status);
        Assert.Equal("invalid", invalidEntry.Code);
        Assert.Equal("Name is required.", invalidEntry.DetailTemplate);

        var errorEntry = Assert.Single(entries, static entry => entry.MemberName == "SaveFailed");
        Assert.Equal("Error", errorEntry.Status);
        Assert.Equal("error", errorEntry.Code);
    }

    [Fact]
    public void Build_Ignores_Non_Static_Error_Provider_Classes()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class UserErrors
            {
                public static Result MissingName() => Result.Invalid(
                    detail: "Name is required.",
                    field: "Name",
                    message: "Name is required.");
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.Single();
        var classDeclaration = syntaxTree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();

        // Act
        var entries = ErrorCatalogModelBuilder.Build(
            classDeclaration,
            compilation.GetSemanticModel(syntaxTree),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public void Emit_Writes_Auto_Generated_Header_And_Provider_Class()
    {
        // Arrange
        var entries = new[]
        {
            new ErrorCatalogEntryModel(
                "demo-usererrors-missingname",
                "docs/errors/README.md",
                "Demo.UserErrors",
                "MissingName",
                "Invalid",
                "invalid",
                "Name is required.",
                "Name is required.")
        };

        // Act
        var generated = ErrorCatalogEmitter.Emit("GeneratedDemoResultErrorCatalogProvider", entries);

        // Assert
        Assert.Contains("// <auto-generated />", generated, StringComparison.Ordinal);
        Assert.Contains("GeneratedDemoResultErrorCatalogProvider", generated, StringComparison.Ordinal);
        Assert.Contains("public global::System.Collections.Generic.IReadOnlyList<ResultErrorCatalogEntry> Entries => ResultErrorCatalogGenerated.generatedEntries;", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Demo.Admin.Domain", "GeneratedDemoAdminDomainResultErrorCatalogProvider")]
    [InlineData("demo-admin.domain", "GenerateddemoadmindomainResultErrorCatalogProvider")]
    public void SanitizeProviderTypeName_Removes_Non_Alphanumeric_Characters(string assemblyName, string expected)
    {
        var actual = ResultErrorCatalogGenerator.SanitizeProviderTypeName(assemblyName);

        Assert.Equal(expected, actual);
    }
}
