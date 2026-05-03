using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace SharedKernel.Mediator.CodeFixes.Tests;

/// <summary>
/// Verifies safe fixes for the currently implemented mediator diagnostics.
/// </summary>
public sealed class SharedKernelMediatorCodeFixProviderTests
{
    private const string MissingArgumentDiagnosticId = "CS7036";
    private const string InvalidRequestArgumentDiagnosticId = "CS1503";

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
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("MissingTourHandler.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnosticsAsync();

        // Assert
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("IQueryHandler<global::Demo.MissingTour, string>", generatedHandlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.MissingHandler);
    }

    [Fact]
    public void Fixable_Diagnostic_Ids_Match_Registered_Mediator_Fixes()
    {
        // Arrange
        var provider = new SharedKernelMediatorCodeFixProvider();

        // Act
        var diagnosticIds = provider.FixableDiagnosticIds
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(
            [
                InvalidRequestArgumentDiagnosticId,
                MissingArgumentDiagnosticId,
                MediatorDiagnosticIds.MissingHandler,
                MediatorDiagnosticIds.InvalidHandlerSignature,
                MediatorDiagnosticIds.MissingCancellationToken,
                MediatorDiagnosticIds.MissingCancellationForwarding,
                MediatorDiagnosticIds.InaccessibleRegistrationType,
                MediatorDiagnosticIds.MissingModuleMarker,
            ],
            diagnosticIds);
    }

    [Fact]
    public void Fix_All_Is_Advertised_Only_For_Safe_Diagnostics()
    {
        // Arrange
        var provider = new SharedKernelMediatorCodeFixProvider();

        // Act
        var supportedDiagnosticIds = provider.GetFixAllProvider()
            .GetSupportedFixAllDiagnosticIds(provider)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(
            [
                InvalidRequestArgumentDiagnosticId,
                MissingArgumentDiagnosticId,
                MediatorDiagnosticIds.MissingHandler,
                MediatorDiagnosticIds.MissingCancellationToken,
                MediatorDiagnosticIds.MissingModuleMarker,
            ],
            supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.InvalidHandlerSignature, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.MissingCancellationForwarding, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.InaccessibleRegistrationType, supportedDiagnosticIds);
    }

    [Fact]
    public void Fix_All_Provider_Throws_When_Original_Provider_Is_Null()
    {
        // Arrange
        var fixAllProvider = new SharedKernelMediatorCodeFixProvider().GetFixAllProvider();

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(() => fixAllProvider.GetSupportedFixAllDiagnosticIds(null!));
        Assert.Equal("originalCodeFixProvider", exception.ParamName);
    }

    [Fact]
    public void Fix_All_Provider_Exposes_The_Batch_Fixer_Scopes()
    {
        // Arrange
        var fixAllProvider = new SharedKernelMediatorCodeFixProvider().GetFixAllProvider();

        // Act
        var supportedScopes = fixAllProvider.GetSupportedFixAllScopes()
            .OrderBy(static scope => scope)
            .ToArray();
        var expectedScopes = WellKnownFixAllProviders.BatchFixer
            .GetSupportedFixAllScopes()
            .OrderBy(static scope => scope)
            .ToArray();

        // Assert
        Assert.Equal(expectedScopes, supportedScopes);
    }

    [Fact]
    public async Task Missing_Request_Interface_Adds_IRequest_Response_Type()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id);

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<string> Load(CancellationToken ct)
                {
                    return sender.Send<string>(new MissingTour(42), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add IRequest<string>", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_Adds_IQuery_Response_Type_When_Query_Handler_Exists()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id);

            public sealed class MissingTourHandler : IQueryHandler<MissingTour, string>
            {
                public ValueTask<string> Handle(MissingTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Id.ToString());
                }
            }

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<string> Load(CancellationToken ct)
                {
                    return sender.Send<string>(new MissingTour(42), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add IQuery<string>", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IQuery<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_Adds_ICommand_Response_Type_When_Command_Handler_Exists()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name);

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Name.Length);
                }
            }

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<int> Create(CancellationToken ct)
                {
                    return sender.Send<int>(new CreateTour("Rome"), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "CreateTour(\"Rome\")");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add ICommand<int>", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record CreateTour(string Name) : global::SharedKernel.Mediator.ICommand<int>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_Adds_ICommand_When_Void_Command_Handler_Exists()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(int Id);

            public sealed class ArchiveTourHandler : ICommandHandler<ArchiveTour>
            {
                public ValueTask<Unit> Handle(ArchiveTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(Unit.Value);
                }
            }

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<Unit> Archive(CancellationToken ct)
                {
                    return sender.Send<Unit>(new ArchiveTour(42), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "ArchiveTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add ICommand", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record ArchiveTour(int Id) : global::SharedKernel.Mediator.ICommand;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_Does_Not_Register_When_Request_Already_Implements_A_Mediator_Interface()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id) : IRequest<string>;

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<string> Load(CancellationToken ct)
                {
                    return sender.Send<string>(new MissingTour(42), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeActions = await workspace.GetCodeActionsAsync(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Missing_Request_Interface_Does_Not_Register_For_Non_Send_Invocation()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id);

            public sealed class TourFacade
            {
                public ValueTask<string> Load(MissingTour request, CancellationToken ct)
                {
                    return Handle(request, ct);
                }

                private static ValueTask<string> Handle(MissingTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Id.ToString());
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "request");

        // Act
        var codeActions = await workspace.GetCodeActionsAsync(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Missing_Request_Generates_Handler_File_From_Block_Scoped_Namespace()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo
            {
                public sealed record MissingTour(int Id) : IQuery<string>;
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("MissingTourHandler.cs");

        // Assert
        Assert.Contains("namespace Demo;", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Generates_Handler_File_When_Global_Usings_Define_Mediator_Types()
    {
        // Arrange
        const string globalUsingsSource = """
            global using SharedKernel.Mediator;
            global using System.Threading;
            global using System.Threading.Tasks;
            """;
        const string source = """
            namespace Demo;

            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var workspace = CodeFixTestWorkspace.CreateWithGlobalUsings(source, globalUsingsSource);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("MissingTourHandler.cs");

        // Assert
        Assert.Contains("namespace Demo;", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Generates_Handler_File_With_Nullable_Response_Type()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id) : IQuery<string?>;
            """;
        var workspace = CodeFixTestWorkspace.CreateWithNullableEnabled(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("MissingTourHandler.cs");

        // Assert
        Assert.Contains("IQueryHandler<global::Demo.MissingTour, string?>", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("ValueTask<string?>", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Generates_IRequest_Handler_File()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IRequest<string>;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("LookupTourHandler.cs");

        // Assert
        Assert.Contains(
            "public sealed class LookupTourHandler : global::SharedKernel.Mediator.IRequestHandler<global::Demo.LookupTour, string>",
            generatedHandlerSource,
            StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<string> Handle(", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Generates_ICommand_Response_Handler_File()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("CreateTourHandler.cs");

        // Assert
        Assert.Contains(
            "public sealed class CreateTourHandler : global::SharedKernel.Mediator.ICommandHandler<global::Demo.CreateTour, int>",
            generatedHandlerSource,
            StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<int> Handle(", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Generates_ICommand_Handler_File()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(int Id) : ICommand;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentTextAsync("ArchiveTourHandler.cs");

        // Assert
        Assert.Contains(
            "public sealed class ArchiveTourHandler : global::SharedKernel.Mediator.ICommandHandler<global::Demo.ArchiveTour>",
            generatedHandlerSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<global::SharedKernel.Mediator.Unit> Handle(",
            generatedHandlerSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Does_Not_Generate_Handler_When_A_Handler_Already_Exists()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ExistingTour(int Id) : IQuery<string>;

            public sealed class ExistingTourHandler : IQueryHandler<ExistingTour, string>
            {
                public ValueTask<string> Handle(ExistingTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Id.ToString());
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            MediatorDiagnosticIds.MissingHandler,
            "Test0.cs",
            "ExistingTour");

        // Act
        var codeActions = await workspace.GetCodeActionsAsync(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Explicit_Interface_Handler_In_Block_Scoped_Namespace_Adds_Public_Forwarding_Handle_Method()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo
            {
                public sealed record LookupTour(string Code) : IQuery<string>;

                public sealed class ExplicitLookupTourHandler : IQueryHandler<LookupTour, string>
                {
                    ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                    {
                        return ValueTask.FromResult(request.Code);
                    }
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<string> Handle(global::Demo.LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.Contains(
            "return ((global::SharedKernel.Mediator.IQueryHandler<global::Demo.LookupTour, string>)this).Handle(request, ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_Interface_Handler_With_Global_Usings_Adds_Public_Forwarding_Handle_Method()
    {
        // Arrange
        const string globalUsingsSource = """
            global using SharedKernel.Mediator;
            global using System.Threading;
            global using System.Threading.Tasks;
            """;
        const string source = """
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
        var workspace = CodeFixTestWorkspace.CreateWithGlobalUsings(source, globalUsingsSource);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<string> Handle(global::Demo.LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.Contains(
            "return ((global::SharedKernel.Mediator.IQueryHandler<global::Demo.LookupTour, string>)this).Handle(request, ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_Interface_Handler_With_Nullable_Response_Type_Adds_Public_Forwarding_Handle_Method()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string?>;

            public sealed class ExplicitLookupTourHandler : IQueryHandler<LookupTour, string?>
            {
                ValueTask<string?> IQueryHandler<LookupTour, string?>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult<string?>(request.Code);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.CreateWithNullableEnabled(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<string?> Handle(global::Demo.LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.Contains(
            "return ((global::SharedKernel.Mediator.IQueryHandler<global::Demo.LookupTour, string?>)this).Handle(request, ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_In_Block_Scoped_Namespace_Adds_IRequest_Response_Type()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo
            {
                public sealed record MissingTour(int Id);

                public sealed class TourFacade(ISender sender)
                {
                    public ValueTask<string> Load(CancellationToken ct)
                    {
                        return sender.Send<string>(new MissingTour(42), ct);
                    }
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add IRequest<string>", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_With_Global_Usings_Adds_IRequest_Response_Type()
    {
        // Arrange
        const string globalUsingsSource = """
            global using SharedKernel.Mediator;
            global using System.Threading;
            global using System.Threading.Tasks;
            """;
        const string source = """
            namespace Demo;

            public sealed record MissingTour(int Id);

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<string> Load(CancellationToken ct)
                {
                    return sender.Send<string>(new MissingTour(42), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.CreateWithGlobalUsings(source, globalUsingsSource);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add IRequest<string>", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_Request_Interface_With_Nullable_Response_Type_Adds_IRequest_Response_Type()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id);

            public sealed class TourFacade(ISender sender)
            {
                public ValueTask<string?> Load(CancellationToken ct)
                {
                    return sender.Send<string?>(new MissingTour(42), ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.CreateWithNullableEnabled(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add IRequest<string?>", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string?>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_Interface_Request_Handler_Adds_Public_Forwarding_Handle_Method()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IRequest<string>;

            public sealed class ExplicitLookupTourHandler : IRequestHandler<LookupTour, string>
            {
                ValueTask<string> IRequestHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<string> Handle(global::Demo.LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.Contains(
            "return ((global::SharedKernel.Mediator.IRequestHandler<global::Demo.LookupTour, string>)this).Handle(request, ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_Interface_Command_Handler_Adds_Public_Forwarding_Handle_Method()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(int Id) : ICommand;

            public sealed class ArchiveTourHandler : ICommandHandler<ArchiveTour>
            {
                ValueTask<Unit> ICommandHandler<ArchiveTour>.Handle(ArchiveTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(Unit.Value);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActionsAsync(provider, diagnostic));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public global::System.Threading.Tasks.ValueTask<global::SharedKernel.Mediator.Unit> Handle(global::Demo.ArchiveTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
        Assert.Contains(
            "return ((global::SharedKernel.Mediator.ICommandHandler<global::Demo.ArchiveTour>)this).Handle(request, ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_Interface_Handler_Does_Not_Register_When_A_Public_Handle_Method_Already_Exists()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class ExplicitLookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }

                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return Handle(request, ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            MediatorDiagnosticIds.InvalidHandlerSignature,
            "Test0.cs",
            "ExplicitLookupTourHandler");

        // Act
        var codeActions = await workspace.GetCodeActionsAsync(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnosticAsync(MediatorDiagnosticIds.InvalidHandlerSignature);

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
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.InvalidHandlerSignature);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.MissingHandler);
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
            MediatorDiagnosticIds.InaccessibleRegistrationType,
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
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.InaccessibleRegistrationType);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_CancellationToken_Adds_Parameter_To_Public_Handle_Method()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request)
                {
                    return ValueTask.FromResult(request.Code);
                }

                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            MediatorDiagnosticIds.MissingCancellationToken,
            "Test0.cs",
            "Handle(LookupTour request)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Add CancellationToken ct parameter", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "public ValueTask<string> Handle(LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_CancellationToken_Forwarding_Adds_Ct_Argument()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return await sender.Send(new SearchTour(request.Code));
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            MissingArgumentDiagnosticId,
            "Test0.cs",
            "sender.Send(new SearchTour(request.Code))");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Forward CancellationToken ct", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "return await sender.Send(new SearchTour(request.Code), ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_CancellationToken_Forwarding_Replaces_Argument_With_Ct()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return await sender.Send(new SearchTour(request.Code), CancellationToken.None);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnosticAsync(
            MediatorDiagnosticIds.MissingCancellationForwarding,
            "Test0.cs",
            "CancellationToken.None");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActionsAsync(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, "Forward CancellationToken ct", StringComparison.Ordinal));
        await workspace.ApplyCodeActionAsync(codeAction);
        var updatedDocumentText = await workspace.GetDocumentTextAsync();

        // Assert
        Assert.Contains(
            "return await sender.Send(new SearchTour(request.Code), ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
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
            MediatorDiagnosticIds.InaccessibleRegistrationType,
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
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.InaccessibleRegistrationType);
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
            MediatorDiagnosticIds.MissingModuleMarker,
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
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.MissingModuleMarker);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }
}
