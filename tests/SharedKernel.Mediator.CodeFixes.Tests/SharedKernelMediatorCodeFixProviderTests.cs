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
}
