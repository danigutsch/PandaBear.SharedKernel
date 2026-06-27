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
    public async Task Missing_request_generates_handler_file()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("MissingTourHandler.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnostics();

        // Assert
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("IQueryHandler<global::Demo.MissingTour, string>", generatedHandlerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.MissingHandler);
    }

    [Fact]
    public void Fixable_diagnostic_ids_match_registered_mediator_fixes()
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
                MediatorDiagnosticIds.MissingEnumeratorCancellation,
                MediatorDiagnosticIds.InaccessibleRegistrationType,
                MediatorDiagnosticIds.MissingModuleMarker,
            ],
            diagnosticIds);
    }

    [Fact]
    public void Fix_all_is_advertised_only_for_safe_diagnostics()
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
                MediatorDiagnosticIds.MissingEnumeratorCancellation,
                MediatorDiagnosticIds.MissingModuleMarker,
            ],
            supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.InvalidHandlerSignature, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.MissingCancellationForwarding, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.InaccessibleRegistrationType, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.DuplicateNotificationHandlerOrder, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.InvalidPipelineGenericArity, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.DuplicatePipelineOrder, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.NeverAppliesPipeline, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.UnboundPipelineConstraints, supportedDiagnosticIds);
        Assert.DoesNotContain(MediatorDiagnosticIds.HandlerShouldNotCallSender, supportedDiagnosticIds);
    }

    [Fact]
    public void Fix_all_provider_throws_when_original_provider_is_null()
    {
        // Arrange
        var fixAllProvider = new SharedKernelMediatorCodeFixProvider().GetFixAllProvider();

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(() => fixAllProvider.GetSupportedFixAllDiagnosticIds(null!));
        Assert.Equal("originalCodeFixProvider", exception.ParamName);
    }

    [Fact]
    public void Fix_all_provider_exposes_the_batch_fixer_scopes()
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
    public async Task Missing_request_interface_adds_iRequest_response_type()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("IRequest<string>"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_adds_iQuery_response_type_when_query_handler_exists()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("IQuery<string>"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IQuery<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_adds_iCommand_response_type_when_command_handler_exists()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "CreateTour(\"Rome\")");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("ICommand<int>"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record CreateTour(string Name) : global::SharedKernel.Mediator.ICommand<int>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_adds_iCommand_when_void_command_handler_exists()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "ArchiveTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("ICommand"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record ArchiveTour(int Id) : global::SharedKernel.Mediator.ICommand;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_does_not_register_when_request_already_implements_a_mediator_interface()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Missing_request_interface_does_not_register_for_non_send_invocation()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "request");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Missing_request_generates_handler_file_from_block_scoped_namespace()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("MissingTourHandler.cs");

        // Assert
        Assert.Contains("namespace Demo;", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_generates_handler_file_when_global_usings_define_mediator_types()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("MissingTourHandler.cs");

        // Assert
        Assert.Contains("namespace Demo;", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class MissingTourHandler", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_generates_handler_file_with_nullable_response_type()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record MissingTour(int Id) : IQuery<string?>;
            """;
        var workspace = CodeFixTestWorkspace.CreateWithNullableEnabled(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("MissingTourHandler.cs");

        // Assert
        Assert.Contains("IQueryHandler<global::Demo.MissingTour, string?>", generatedHandlerSource, StringComparison.Ordinal);
        Assert.Contains("ValueTask<string?>", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_generates_iRequest_handler_file()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IRequest<string>;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("LookupTourHandler.cs");

        // Assert
        Assert.Contains(
            "public sealed class LookupTourHandler : global::SharedKernel.Mediator.IRequestHandler<global::Demo.LookupTour, string>",
            generatedHandlerSource,
            StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<string> Handle(", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_generates_iCommand_response_handler_file()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("CreateTourHandler.cs");

        // Assert
        Assert.Contains(
            "public sealed class CreateTourHandler : global::SharedKernel.Mediator.ICommandHandler<global::Demo.CreateTour, int>",
            generatedHandlerSource,
            StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<int> Handle(", generatedHandlerSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_generates_iCommand_handler_file()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(int Id) : ICommand;
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.MissingHandler);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var generatedHandlerSource = await workspace.GetAdditionalDocumentText("ArchiveTourHandler.cs");

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
    public async Task Missing_request_does_not_generate_handler_when_a_handler_already_exists()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingHandler,
            "Test0.cs",
            "ExistingTour");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Explicit_interface_handler_in_block_scoped_namespace_adds_public_forwarding_handle_method()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

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
    public async Task Explicit_interface_handler_with_global_usings_adds_public_forwarding_handle_method()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

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
    public async Task Explicit_interface_handler_with_nullable_response_type_adds_public_forwarding_handle_method()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

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
    public async Task Missing_request_interface_in_block_scoped_namespace_adds_iRequest_response_type()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("IRequest<string>"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_with_global_usings_adds_iRequest_response_type()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("IRequest<string>"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_with_nullable_response_type_adds_iRequest_response_type()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "MissingTour(42)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddRequestInterface("IRequest<string?>"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public sealed record MissingTour(int Id) : global::SharedKernel.Mediator.IRequest<string?>;",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_interface_request_handler_adds_public_forwarding_handle_method()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

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
    public async Task Explicit_interface_command_handler_adds_public_forwarding_handle_method()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

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
    public async Task Explicit_interface_handler_does_not_register_when_a_public_handle_method_already_exists()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.InvalidHandlerSignature,
            "Test0.cs",
            "ExplicitLookupTourHandler");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Explicit_interface_handler_adds_public_forwarding_handle_method()
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
        var diagnostic = await workspace.GetSingleGeneratorDiagnostic(MediatorDiagnosticIds.InvalidHandlerSignature);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnostics();

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
    public async Task Inaccessible_module_handler_can_be_made_public()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.InaccessibleRegistrationType,
            "Module.cs",
            "SearchToursHandler",
            ImmutableDictionary<string, string?>.Empty.Add("PrimaryAssemblyName", "SharedKernel.Mediator.CodeFixes.Tests.Primary"));

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.MakeTypePublic, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedModuleSource = await workspace.GetDocumentText("Module.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnostics();
        var generatedSource = await workspace.GetGeneratedSource("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("public sealed class SearchToursHandler", updatedModuleSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.InaccessibleRegistrationType);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cancellationToken_adds_parameter_to_public_handle_method()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingCancellationToken,
            "Test0.cs",
            "Handle(LookupTour request)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddCancellationTokenParameter, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public ValueTask<string> Handle(LookupTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cancellationToken_forwarding_adds_ct_argument()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MissingArgumentDiagnosticId,
            "Test0.cs",
            "sender.Send(new SearchTour(request.Code))");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.ForwardCancellationToken("ct"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "return await sender.Send(new SearchTour(request.Code), ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_enumeratorCancellation_adds_attribute_to_stream_handler()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingEnumeratorCancellation,
            "Test0.cs",
            "CancellationToken ct");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddEnumeratorCancellation, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "[global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_enumeratorCancellation_adds_attribute_to_stream_pipeline()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, StreamHandlerContinuation<string> next, CancellationToken ct)
                {
                    await Task.Yield();
                    await foreach (var item in next())
                    {
                        yield return item;
                    }
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingEnumeratorCancellation,
            "Test0.cs",
            "CancellationToken ct");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddEnumeratorCancellation, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "[global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_cancellationToken_forwarding_replaces_argument_with_ct()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingCancellationForwarding,
            "Test0.cs",
            "CancellationToken.None");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.ForwardCancellationToken("ct"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "return await sender.Send(new SearchTour(request.Code), ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inaccessible_module_handler_can_add_internalsVisibleTo()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.InaccessibleRegistrationType,
            "Module.cs",
            "SearchToursHandler",
            ImmutableDictionary<string, string?>.Empty.Add("PrimaryAssemblyName", "SharedKernel.Mediator.CodeFixes.Tests.Primary"));

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => candidate.Title.Contains("InternalsVisibleTo", StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var friendAssemblySource = await workspace.GetDocumentText(
            "MediatorInternalsVisibleTo.SharedKernel.Mediator.CodeFixes.Tests.Primary.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnostics();
        var generatedSource = await workspace.GetGeneratedSource("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains(
            "[assembly: global::System.Runtime.CompilerServices.InternalsVisibleTo(\"SharedKernel.Mediator.CodeFixes.Tests.Primary\")]",
            friendAssemblySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.InaccessibleRegistrationType);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unmarked_module_can_add_mediator_module_assembly_attribute()
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingModuleMarker,
            "Module.cs",
            "SearchTours");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddMediatorModuleAttribute, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var markerSource = await workspace.GetDocumentText("MediatorModuleAssemblyInfo.cs");
        var diagnosticsAfterFix = await workspace.GetGeneratorDiagnostics();
        var generatedSource = await workspace.GetGeneratedSource("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("[assembly: global::SharedKernel.Mediator.MediatorModuleAttribute]", markerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnosticsAfterFix, static candidate => candidate.Id == MediatorDiagnosticIds.MissingModuleMarker);
        Assert.Contains("services.AddTransient<global::ModuleA.SearchToursHandler>();", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cancellationToken_forwarding_title_uses_actual_parameter_name()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken cancellationToken)
                {
                    return await sender.Send(new SearchTour(request.Code));
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MissingArgumentDiagnosticId,
            "Test0.cs",
            "sender.Send(new SearchTour(request.Code))");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.ForwardCancellationToken("cancellationToken"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "return await sender.Send(new SearchTour(request.Code), cancellationToken);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cancellationToken_forwarding_does_not_register_when_no_cancellationToken_in_scope()
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
                    return await CallInner();
                }

                private async ValueTask<string> CallInner()
                {
                    return await sender.Send(new SearchTour("x"));
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MissingArgumentDiagnosticId,
            "Test0.cs",
            "sender.Send(new SearchTour(\"x\"))");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Missing_enumeratorCancellation_adds_attribute_with_non_ct_parameter_name()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingEnumeratorCancellation,
            "Test0.cs",
            "CancellationToken cancellationToken");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddEnumeratorCancellation, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "[global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cancellationToken_adds_parameter_to_void_command_handler()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(string Code) : ICommand;

            public sealed class ArchiveTourHandler : ICommandHandler<ArchiveTour>
            {
                public ValueTask<Unit> Handle(ArchiveTour request)
                {
                    return ValueTask.FromResult(Unit.Value);
                }

                ValueTask<Unit> ICommandHandler<ArchiveTour>.Handle(ArchiveTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(Unit.Value);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingCancellationToken,
            "Test0.cs",
            "Handle(ArchiveTour request)");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.AddCancellationTokenParameter, StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "public ValueTask<Unit> Handle(ArchiveTour request, global::System.Threading.CancellationToken ct)",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cancellationToken_does_not_register_when_method_name_is_not_handle()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(string Code) : ICommand;

            public sealed class ArchiveTourHandler : ICommandHandler<ArchiveTour>
            {
                public ValueTask<Unit> Execute(ArchiveTour request)
                {
                    return ValueTask.FromResult(Unit.Value);
                }

                ValueTask<Unit> ICommandHandler<ArchiveTour>.Handle(ArchiveTour request, CancellationToken ct)
                {
                    return Execute(request);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MediatorDiagnosticIds.MissingCancellationToken,
            "Test0.cs",
            "Execute(ArchiveTour request)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Missing_cancellationToken_forwarding_adds_ct_argument_for_publish()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record ArchiveTour(string Code) : ICommand;
            public sealed record TourArchived(string Code) : INotification;

            public sealed class ArchiveTourHandler(IPublisher publisher) : ICommandHandler<ArchiveTour>
            {
                public async ValueTask<Unit> Handle(ArchiveTour request, CancellationToken ct)
                {
                    await publisher.Publish(new TourArchived(request.Code));
                    return Unit.Value;
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelMediatorCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            MissingArgumentDiagnosticId,
            "Test0.cs",
            "publisher.Publish(new TourArchived(request.Code))");

        // Act
        var codeAction = Assert.Single(
            await workspace.GetCodeActions(provider, diagnostic),
            static candidate => string.Equals(candidate.Title, CodeFixTitles.ForwardCancellationToken("ct"), StringComparison.Ordinal));
        await workspace.ApplyCodeAction(codeAction);
        var updatedDocumentText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(
            "await publisher.Publish(new TourArchived(request.Code), ct);",
            updatedDocumentText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_request_interface_does_not_register_when_request_implements_iCommand_of_response()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

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
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            InvalidRequestArgumentDiagnosticId,
            "Test0.cs",
            "CreateTour(\"Rome\")");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }
}
