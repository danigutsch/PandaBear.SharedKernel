using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Style.Analyzers.Tests;

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
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value").ShouldNotBeEmpty();
    }

    [Fact]
    public void Canonical_owner_and_unrelated_duplicates_are_allowed()
    {
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string One = \"unrelated\"; public const string Two = One; }", assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value").ShouldBeEmpty();
    }

    [Fact]
    public void Documented_compatibility_alias_is_allowed()
    {
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string Alias = Value; }", assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value\nregistered|Production|Demo.Keys.Alias|Preserve published API until the next major version.").ShouldBeEmpty();
    }

    [Fact]
    public void Stale_exception_is_rejected()
    {
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; }", assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value\nregistered|Production|Demo.Keys.Alias|Preserve published API.").ShouldNotBeEmpty();
    }
    [Fact]
    public void Cross_assembly_redirects_are_rejected()
    {
        var owner = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; }", assemblyName: "Owner");
        var consumer = AnalyzerTestHarness.CreateCompilation("public static class Redirect { public const string Value = Demo.Keys.Value; }", assemblyName: "Consumer")
            .AddReferences(owner.ToMetadataReference());
        GovernedConstantGuard.Validate([owner, consumer], "registered|Owner|Demo.Keys.Value")
            .ShouldContain(error => error.Contains("Consumer:Redirect.Value", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_duplicates_are_exempt()
    {
        var owner = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; }", assemblyName: "Production");
        var generated = AnalyzerTestHarness.CreateCompilation("public static class Generated { public const string Value = \"registered\"; }", path: "Generated.g.cs");
        GovernedConstantGuard.Validate([owner, generated], "registered|Production|Demo.Keys.Value").ShouldBeEmpty();
    }

    [Fact]
    public void Unresolved_alias_fails_closed()
    {
        var compilation = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = Unknown.Value; }", assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value")
            .ShouldContain(error => error.Contains("Cannot resolve constant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("registered|Production|Demo.Keys.Value| ")]
    [InlineData("unregistered|Production|Demo.Keys.Alias|Compatibility")]
    public void Invalid_exceptions_are_rejected(string catalog)
    {
        Action parse = () => GovernedLiteralCatalog.Parse(catalog);
        parse.ShouldThrow<FormatException>();
    }
    [Fact]
    public void Conditional_production_redirect_is_rejected()
    {
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
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value")
            .ShouldContain(error => error.Contains("Demo.Keys.Alias", StringComparison.Ordinal));
    }

    [Fact]
    public void Unrelated_external_constant_is_allowed_with_resolved_references()
    {
        var external = AnalyzerTestHarness.CreateCompilation("public static class External { public const string Value = \"unrelated\"; }", assemblyName: "External");
        var consumer = AnalyzerTestHarness.CreateCompilation("namespace Demo; public static class Keys { public const string Value = \"registered\"; public const string Other = External.Value; }", assemblyName: "Production")
            .AddReferences(external.ToMetadataReference());
        GovernedConstantGuard.Validate([consumer], "registered|Production|Demo.Keys.Value").ShouldBeEmpty();
    }
    [Theory]
    [InlineData("Value")]
    [InlineData("\"registered\"")]
    public void Local_governed_constants_are_rejected(string initializer)
    {
        var source = $"namespace Demo; public static class Keys {{ public const string Value = \"registered\"; public static string Read() {{ const string Alias = {initializer}; return Alias; }} }}";
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value")
            .ShouldContain(error => error.Contains("Alias", StringComparison.Ordinal));
    }

    [Fact]
    public void Nested_types_in_generated_parent_are_exempt()
    {
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
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value").ShouldBeEmpty();
    }

    [Fact]
    public void Unregistered_local_constants_are_allowed()
    {
        const string source = "namespace Demo; public static class Keys { public const string Value = \"registered\"; public static string Read() { const string Other = \"unrelated\"; return Other; } }";
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");
        GovernedConstantGuard.Validate([compilation], "registered|Production|Demo.Keys.Value").ShouldBeEmpty();
    }
}
