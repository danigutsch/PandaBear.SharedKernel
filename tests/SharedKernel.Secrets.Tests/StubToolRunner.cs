using System.Text;

namespace SharedKernel.Secrets.Tests;

/// <summary>An <see cref="IToolRunner"/> that answers every run with one exit code and error text, or throws.</summary>
internal sealed class StubToolRunner(int exitCode, string standardError, Exception? failure = null) : IToolRunner
{
    /// <inheritdoc />
    public Task<ToolResult> Run(ToolRequest request, CancellationToken cancellationToken) =>
        failure is null
            ? Task.FromResult(new ToolResult(exitCode, SecretBytes.CopyOf(Encoding.UTF8.GetBytes("output")), standardError))
            : Task.FromException<ToolResult>(failure);

    /// <inheritdoc />
    public Task<int> RunAttached(ToolRequest request, CancellationToken cancellationToken) => Task.FromResult(exitCode);
}
