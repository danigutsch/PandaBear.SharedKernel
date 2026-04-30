using System.Collections.Immutable;

namespace SharedKernel.Mediator.CodeFixes.Tests;

/// <summary>
/// Verifies safe fixes for the currently implemented mediator diagnostics.
/// </summary>
public sealed class SharedKernelMediatorCodeFixProviderTests
{
    [Fact]
    public async Task Missing_Request_Generates_Handler_File()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync("SKMED001");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("MissingTourHandler.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnosticsAsync();

        // Assert
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("IQueryHandler<global::Demo.MissingTour, string>", generatedHandlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == "SKMED001");
    }

    [Fact]
    public async Task Explicit_Interface_Handler_Adds_Public_Forwarding_Handle_Method()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class ExplicitLookupTourHandler : IQueryHandler<LookupTour, string>
            {
                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync("SKMED003");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnosticsAsync();

        // Assert
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<string> Handle(global::Demo.LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.Contains(
            "return ((global::SharedKernel.Mediator.IQueryHandler<global::Demo.LookupTour, string>)this).Handle(request, ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == "SKMED003");
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == "SKMED001");
    }

    [Fact]
    public async Task Inaccessible_Module_Handler_Can_Be_Made_Public()
    {
        // Arrange
        const string moduleSource = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            internal sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
            {
                public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
            }
            """;
        const string primarySource = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }
            """;
        var workspace = CodeFixTestWorkspace.CreateWithProjectReference(primarySource, moduleSource);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            "SKMED010",
            "Module.cs",
            "SearchToursHandler",
            ImmutableDictionary<string, string?>.Empty.Add("PrimaryAssemblyName", "SharedKernel.Mediator.CodeFixes.Tests.Primary"));

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Make type public", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedModuleSource = await workspace.GetDocumentTextAsync("Module.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnosticsAsync();
        var generatedSource = await workspace.GetGeneratedSourceAsync("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("public sealed class SearchToursHandler", updatedModuleSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == "SKMED010");
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inaccessible_Module_Handler_Can_Add_InternalsVisibleTo()
    {
        // Arrange
        const string moduleSource = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            internal sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
            {
                public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
            }
            """;
        const string primarySource = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }
            """;
        var workspace = CodeFixTestWorkspace.CreateWithProjectReference(primarySource, moduleSource);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            "SKMED010",
            "Module.cs",
            "SearchToursHandler",
            ImmutableDictionary<string, string?>.Empty.Add("PrimaryAssemblyName", "SharedKernel.Mediator.CodeFixes.Tests.Primary"));

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => candidate.Title.Contains("InternalsVisibleTo", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var friendAssemblySource = await workspace.GetDocumentTextAsync(
            "MediatorInternalsVisibleTo.SharedKernel.Mediator.CodeFixes.Tests.Primary.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnosticsAsync();
        var generatedSource = await workspace.GetGeneratedSourceAsync("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains(
            "[assembly: global::System.Runtime.CompilerServices.InternalsVisibleTo(\"SharedKernel.Mediator.CodeFixes.Tests.Primary\")]",
            friendAssemblySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == "SKMED010");
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unmarked_Module_Can_Add_Mediator_Module_Assembly_Attribute()
    {
        // Arrange
        const string moduleSource = """
            using SharedKernel.Mediator;

            namespace ModuleA;

            public sealed record SearchTours(string Query) : IQuery<int>;

            public sealed class SearchToursHandler : IQueryHandler<SearchTours, int>
            {
                public ValueTask<int> Handle(SearchTours request, CancellationToken ct) => ValueTask.FromResult(10);
            }
            """;
        const string primarySource = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }
            """;
        var workspace = CodeFixTestWorkspace.CreateWithProjectReference(primarySource, moduleSource);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            "SKMED011",
            "Module.cs",
            "SearchTours");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add [assembly: MediatorModule]", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var markerSource = await workspace.GetDocumentTextAsync("MediatorModuleAssemblyInfo.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnosticsAsync();
        var generatedSource = await workspace.GetGeneratedSourceAsync("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("[assembly: global::SharedKernel.Mediator.MediatorModuleAttribute]", markerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == "SKMED011");
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }
}
