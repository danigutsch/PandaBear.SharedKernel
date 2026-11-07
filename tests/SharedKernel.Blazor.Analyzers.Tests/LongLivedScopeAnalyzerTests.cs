namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Tests that the analyzer reports components that inject services that go stale in a circuit scope (SKBLZ002) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ComponentStateCapability)]
public sealed class LongLivedScopeAnalyzerTests
{
    [Fact]
    public async Task An_injected_sender_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                [Inject]
                public PandaBear.Mediator.ISender Sender { get; set; } = default!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(BlazorDiagnosticIds.LongLivedScopeService);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain("ISender Sender", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_injected_db_context_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class ShopDb : Microsoft.EntityFrameworkCore.DbContext;

            public sealed class Page : ComponentBase
            {
                [Inject]
                private ShopDb Db { get; set; } = default!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(BlazorDiagnosticIds.LongLivedScopeService);
    }

    [Fact]
    public async Task A_scoped_sender_and_a_db_context_factory_are_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class ShopDb : Microsoft.EntityFrameworkCore.DbContext;

            public sealed class Page : ComponentBase
            {
                [Inject]
                public PandaBear.Mediator.ScopedSender Sender { get; set; } = default!;

                [Inject]
                public Microsoft.EntityFrameworkCore.IDbContextFactory<ShopDb> Factory { get; set; } = default!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_sender_property_without_inject_and_a_non_component_are_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Page : ComponentBase
            {
                public PandaBear.Mediator.ISender? Sender { get; set; }
            }

            public sealed class Service
            {
                [Inject]
                public PandaBear.Mediator.ISender Sender { get; set; } = default!;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
