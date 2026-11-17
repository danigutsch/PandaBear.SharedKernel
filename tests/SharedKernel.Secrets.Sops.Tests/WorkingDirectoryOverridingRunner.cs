namespace PandaBear.Secrets.Sops.Tests;

/// <summary>Runs real processes but starts them in a fixed directory, so a test can prove that an adapter does not depend on where its child starts.</summary>
internal sealed class WorkingDirectoryOverridingRunner(string directory) : IToolRunner
{
    private readonly SystemToolRunner _inner = new();

    /// <inheritdoc />
    public Task<ToolResult> Run(ToolRequest request, CancellationToken cancellationToken) =>
        _inner.Run(new ToolRequest(request.Executable, request.Arguments, request.Environment, request.StandardInput, directory), cancellationToken);

    /// <inheritdoc />
    public Task<int> RunAttached(ToolRequest request, CancellationToken cancellationToken) =>
        _inner.RunAttached(new ToolRequest(request.Executable, request.Arguments, request.Environment, request.StandardInput, directory), cancellationToken);
}
