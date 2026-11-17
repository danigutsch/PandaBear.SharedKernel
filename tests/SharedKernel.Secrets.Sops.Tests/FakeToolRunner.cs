using System.Text;

namespace PandaBear.Secrets.Sops.Tests;

/// <summary>An <see cref="IToolRunner"/> that answers every call with a canned result, or throws, and records the requests.</summary>
internal sealed class FakeToolRunner(Func<ToolRequest, ToolResult> respond) : IToolRunner
{
    /// <summary>Gets the requests in the order they arrived.</summary>
    public List<ToolRequest> Requests { get; } = [];

    /// <summary>Gets or sets what an attached run does with its request and the exit code it returns.</summary>
    public Func<ToolRequest, int> Attached { get; set; } = static _ => throw new InvalidOperationException("No attached run was expected.");

    /// <summary>Gets the standard input bytes of each request, copied when it arrived.</summary>
    public List<byte[]> Inputs { get; } = [];

    /// <summary>Creates a runner whose every call succeeds with <paramref name="standardOutput"/>.</summary>
    public static FakeToolRunner Succeeding(string standardOutput = "", string standardError = "") =>
        new(_ => Result(0, standardOutput, standardError));

    /// <summary>Creates a runner whose every call exits with <paramref name="exitCode"/> and writes <paramref name="standardError"/>.</summary>
    public static FakeToolRunner Failing(int exitCode, string standardError) => new(_ => Result(exitCode, string.Empty, standardError));

    /// <summary>Creates a runner whose every call throws <paramref name="exception"/>.</summary>
    public static FakeToolRunner Throwing(Exception exception) => new(_ => throw exception);

    /// <summary>Builds a result.</summary>
    public static ToolResult Result(int exitCode, string standardOutput, string standardError = "") =>
        new(exitCode, SecretBytes.CopyOf(Encoding.UTF8.GetBytes(standardOutput)), standardError);

    /// <inheritdoc />
    public Task<ToolResult> Run(ToolRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Inputs.Add(request.StandardInput.ToArray());
        return Task.FromResult(respond(request));
    }

    /// <inheritdoc />
    public Task<int> RunAttached(ToolRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Attached(request));
    }
}
