using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>
/// Tests for GovernedConstantGuard, which validates that catalogued literals have one canonical const owner across compilations.
/// </summary>
public sealed class GovernedConstantGuardTests
{
    [Theory]
    [InlineData("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string Duplicate = \"registered\"; }")]
    [InlineData("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string Alias = Value; }")]
    [InlineData("namespace Demo; public static class Keys { public const string Value = \"changed\"; }")]
    [InlineData("namespace Demo; public static class Keys { public static readonly string Value = \"registered\"; }")]
    [InlineData("class Unrelated {}")]
    public void Missing_duplicate_redirected_or_changed_owner_is_rejected(string source)
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Canonical_owner_and_unrelated_duplicates_are_allowed()
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string One = \"unrelated\"; public const string Two = One; }", assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Documented_compatibility_alias_is_allowed()
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string Alias = Value; }", assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value\nregistered|Production|Demo.Keys.Alias|Preserve published API until the next major version.");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Stale_exception_is_rejected()
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; }", assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value\nregistered|Production|Demo.Keys.Alias|Preserve published API.");

        // Assert
        errors.ShouldNotBeEmpty();
    }
    [Fact]
    public void Cross_assembly_redirects_are_rejected()
    {
        // Arrange
        var owner = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; }", assemblyName: "Owner");
        var consumer = AnalyzerTestHarness.CreateCompilation("public static class Redirect { public const string Value = Demo.Keys.Value; }", assemblyName: "Consumer")
            .AddReferences(owner.ToMetadataReference());

        // Act
        var errors = GovernedConstantGuard.Validate([owner, consumer], "registered|Owner|Demo.Keys.Value");

        // Assert
        errors.ShouldContain(error => error.Contains("Consumer:Redirect.Value", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("registered", false)]
    [InlineData("changed", false)]
    [InlineData("registered", true)]
    public void Referenced_canonical_owner_is_checked_once_across_consumers(string value, bool declareAlias)
    {
        // Arrange
        var owner = AnalyzerTestHarness.CreateCompilation(
            $$"""
            namespace Demo;
            public static class Outer
            {
                public static class Keys { public const string Value = "{{value}}"; }
            }
            """, assemblyName: "Owner");
        using var image = new MemoryStream();
        var emit = owner.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        emit.Success.ShouldBeTrue();
        var reference = MetadataReference.CreateFromImage(image.ToArray());
        var source = declareAlias
            ? "public static class Consumer { public const string Alias = Demo.Outer.Keys.Value; }"
            : "public static class Consumer { public static string Read() => Demo.Outer.Keys.Value; }";
        var consumer = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Consumer").AddReferences(reference);
        var secondConsumer = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "SecondConsumer").AddReferences(reference);

        // Act
        var errors = GovernedConstantGuard.Validate([consumer, secondConsumer], "registered|Owner|Demo.Outer.Keys.Value");

        // Assert
        if (value == "changed")
        {
            errors.ShouldHaveSingleItem(error => error.StartsWith("Expected exactly one", StringComparison.Ordinal));
        }
        else if (declareAlias)
        {
            errors.Count.ShouldBe(2);
            errors.ShouldContain(error => error.Contains("Consumer:Consumer.Alias", StringComparison.Ordinal));
            errors.ShouldContain(error => error.Contains("SecondConsumer:Consumer.Alias", StringComparison.Ordinal));
        }
        else
        {
            errors.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData("public static readonly string Value = \"registered\";")]
    [InlineData("public const int Value = 4;")]
    public void Referenced_owner_must_be_a_string_constant(string declaration)
    {
        // Arrange
        var owner = AnalyzerTestHarness.CreateCompilation(
            $"namespace Demo; public static class Keys {{ {declaration} }}", assemblyName: "Owner");
        var consumer = AnalyzerTestHarness.CreateCompilation("", assemblyName: "Consumer")
            .AddReferences(owner.ToMetadataReference());

        // Act
        var errors = GovernedConstantGuard.Validate([consumer], "registered|Owner|Demo.Keys.Value");

        // Assert
        errors.ShouldHaveSingleItem(error => error.StartsWith("Expected exactly one", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_duplicates_are_exempt()
    {
        // Arrange
        var owner = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; }", assemblyName: "Production");
        var generated = AnalyzerTestHarness.CreateCompilation("public static class Generated { public const string Value = \"registered\"; }", path: "Generated.g.cs");

        // Act
        var errors = GovernedConstantGuard.Validate([owner, generated], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Unresolved_alias_fails_closed()
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = Unknown.Value; }", assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldContain(error => error.Contains("Cannot resolve constant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("registered|Production|Demo.Keys.Value| ")]
    [InlineData("unregistered|Production|Demo.Keys.Alias|Compatibility")]
    public void Invalid_exceptions_are_rejected(string catalog)
    {
        // Act
        Action parse = () => GovernedLiteralCatalog.Parse(catalog);

        // Assert
        parse.ShouldThrow<FormatException>();
    }
    [Fact]
    public void Conditional_production_redirect_is_rejected()
    {
        // Arrange
        const string source = """
            namespace Demo;
            public static class Keys
            {
                public const string Value = "registered";
            #if NET10_0
                public const string Alias = Value;
            #endif
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production")
            .RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(source,
                new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET10_0"]), cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldContain(error => error.Contains("Demo.Keys.Alias", StringComparison.Ordinal));
    }

    [Fact]
    public void Unrelated_external_constant_is_allowed_with_resolved_references()
    {
        // Arrange
        var external = AnalyzerTestHarness.CreateCompilation("public static class External { public const string Value = \"unrelated\"; }", assemblyName: "External");
        var consumer = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string Other = External.Value; }", assemblyName: "Production")
            .AddReferences(external.ToMetadataReference());

        // Act
        var errors = GovernedConstantGuard.Validate([consumer], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldBeEmpty();
    }
    [Theory]
    [InlineData("Value")]
    [InlineData("\"registered\"")]
    public void Local_governed_constants_are_rejected(string initializer)
    {
        // Arrange
        var source = $"namespace Demo; public static class Keys {{ public const string Value = \"registered\"; public static string Read() {{ const string Alias = {initializer}; return Alias; }} }}";
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldContain(error => error.Contains("Alias", StringComparison.Ordinal));
    }

    [Fact]
    public void Nested_types_in_generated_parent_are_exempt()
    {
        // Arrange
        const string source = """
            namespace Demo;
            public static class Keys { public const string Value = "registered"; }
            [System.CodeDom.Compiler.GeneratedCode("generator", "1")]
            public static class Generated
            {
                public static class Nested
                {
                    public const string Alias = Keys.Value;
                    public static string Read() { const string Local = Keys.Value; return Local; }
                }
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Unregistered_local_constants_are_allowed()
    {
        // Arrange
        const string source = "namespace Demo; public static class Keys { public const string Value = \"registered\"; public static string Read() { const string Other = \"unrelated\"; return Other; } }";
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");

        // Act
        var errors = GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value");

        // Assert
        errors.ShouldBeEmpty();
    }
}
