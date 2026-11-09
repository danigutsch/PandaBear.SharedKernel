using System.Text;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>An <see cref="IToolRunner"/> that answers with a scripted result and records every request.</summary>
internal sealed class ScriptedToolRunner(Func<ToolRequest, ToolResult> respond) : IToolRunner
{
    /// <summary>Gets the requests in the order they arrived.</summary>
    public List<ToolRequest> Requests { get; } = [];

    /// <summary>Gets the standard input bytes of each request.</summary>
    public List<byte[]> Inputs { get; } = [];

    /// <summary>Creates a runner that is never expected to run anything.</summary>
    public static ScriptedToolRunner Unused() => new(_ => throw new InvalidOperationException("No process was expected."));

    /// <summary>Creates a runner whose every call succeeds with <paramref name="standardOutput"/>.</summary>
    public static ScriptedToolRunner Succeeding(string standardOutput = "", string standardError = "") => new(_ => Result(0, standardOutput, standardError));

    /// <summary>Creates a runner whose every call exits with <paramref name="exitCode"/>.</summary>
    public static ScriptedToolRunner Failing(int exitCode, string standardError) => new(_ => Result(exitCode, string.Empty, standardError));

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
}
