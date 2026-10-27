using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using PandaBear.CodeFixes.Testing;
using PandaBear.Domain.Analyzers;

namespace PandaBear.Domain.CodeFixes.Tests;

/// <summary>Tests the code fix provider that makes non-private domain property setters private.</summary>
public sealed class SharedKernelDomainCodeFixProviderTests
{
    [Theory]
    [InlineData("public int Count { get; set; }", "set;")]
    [InlineData("public int Count { get; internal set; }", "internal set;")]
    [InlineData("public int Count { get; protected internal set; }", "protected internal set;")]
    public async Task Setter_fix_makes_the_setter_private(string propertyDeclaration, string marker)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Tour
            {
                {{propertyDeclaration}}
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, marker);

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);
        var codeAction = offeredActions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();
        var compilationDiagnostics = await workspace.GetCompilationDiagnostics();
        var compilationErrors = compilationDiagnostics.Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        codeAction.EquivalenceKey.ShouldBe("MakeDomainSetterPrivate");
        updatedText.ShouldContain("public int Count { get; private set; }", StringComparison.Ordinal);
        compilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Writes_inside_the_declaring_type_keep_the_fix_available()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed partial class Tour
            {
                public int Count { get; set; }

                public void Increment() => Count++;

                public void Reset()
                {
                    this.Count = 0;
                }

                private sealed class Snapshot
                {
                    public static void Restore(Tour tour) => tour.Count = 1;
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        workspace.AddDocument(
            """
            namespace Demo;

            public sealed partial class Tour
            {
                public void Double() => Count *= 2;
            }
            """,
            "TourPart.cs");
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, "set;");

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);
        var codeAction = offeredActions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();
        var compilationDiagnostics = await workspace.GetCompilationDiagnostics();
        var compilationErrors = compilationDiagnostics.Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        updatedText.ShouldContain("public int Count { get; private set; }", StringComparison.Ordinal);
        compilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reads_outside_the_declaring_type_keep_the_fix_available()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public int Count { get; set; }
            }

            public static class Reader
            {
                public static int Read(Tour tour) => tour.Count;
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, "set;");

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        offeredActions.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("tour.Count = 1;")]
    [InlineData("(tour.Count) = 1;")]
    [InlineData("tour.Count += 1;")]
    [InlineData("tour.Count ??= 1;")]
    [InlineData("tour.Count++;")]
    [InlineData("--tour.Count;")]
    [InlineData("_ = new Tour { Count = 1 };")]
    [InlineData("_ = tour with { Count = 1 };")]
    [InlineData("(tour.Count, _) = (1, 2);")]
    [InlineData("((tour.Count, _), _) = ((1, 2), 3);")]
    [InlineData("tour?.Count = 1;")]
    public async Task Write_outside_the_declaring_type_offers_no_fix(string writeStatement)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed record Tour
            {
                public int? Count { get; set; }
            }

            public static class Writer
            {
                public static void Write(Tour tour)
                {
                    {{writeStatement}}
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, "set;");

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        offeredActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Write_in_another_document_offers_no_fix()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public int Count { get; set; }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        workspace.AddDocument(
            """
            namespace Demo;

            public static class Writer
            {
                public static void Write(Tour tour) => tour.Count = 1;
            }
            """,
            "Writer.cs");
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, "set;");

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        offeredActions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("public abstract class Tour { public abstract int Count { get; set; } }")]
    [InlineData("public class Tour { public virtual int Count { get; set; } }")]
    [InlineData("public sealed class Tour : Entity { public override int Count { get; set; } } public abstract class Entity { public abstract int Count { get; set; } }")]
    [InlineData("public sealed class Tour : ICounted { public int Count { get; set; } } public interface ICounted { int Count { get; set; } }")]
    [InlineData("public sealed class Tour : ICounted { int ICounted.Count { get; set; } } public interface ICounted { int Count { get; set; } }")]
    [InlineData("public interface ICounted { int Count { get; set; } }")]
    public async Task Setter_that_cannot_become_private_offers_no_fix(string declarations)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            {{declarations}}
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, "set;");

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        offeredActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Diagnostic_outside_a_setter_offers_no_fix()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public int Count { get; set; }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelDomainCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DomainDiagnosticIds.NonPrivateSetter, "get;");

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        offeredActions.ShouldBeEmpty();
    }

    [Fact]
    public void Provider_fixes_only_setter_diagnostics_with_batch_fix_all()
    {
        // Arrange
        var provider = new SharedKernelDomainCodeFixProvider();

        // Act
        var fixableDiagnosticIds = provider.FixableDiagnosticIds;
        var fixAllProvider = provider.GetFixAllProvider().ShouldNotBeNull();
        var supportedScopes = fixAllProvider.GetSupportedFixAllScopes().ToArray();
        var batchScopes = WellKnownFixAllProviders.BatchFixer.GetSupportedFixAllScopes().ToArray();

        // Assert
        fixableDiagnosticIds.ShouldHaveSingleItem().ShouldBe(DomainDiagnosticIds.NonPrivateSetter);
        supportedScopes.ShouldBe(batchScopes, "Each setter fix is independent, so the batch fixer can merge them.");
    }
}
