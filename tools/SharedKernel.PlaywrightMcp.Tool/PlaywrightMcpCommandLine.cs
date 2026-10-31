using System.CommandLine;
using System.Diagnostics;

namespace PandaBear.PlaywrightMcp.Tool;

/// <summary>Builds the command-line interface of the Playwright MCP tool.</summary>
internal static class PlaywrightMcpCommandLine
{
    private const string Description = $"""
        Run the pinned Playwright MCP image on local Docker or rootless Podman.
        Image: {PlaywrightMcpCommand.Image}

        Environment variables:
          PLAYWRIGHT_MCP_ENGINE           docker or podman; required when both are installed
          PLAYWRIGHT_MCP_NETWORK_ACCESS   1 enables container networking; default 0
          PLAYWRIGHT_MCP_IMAGE_RESPONSES  allow sends images to the MCP client; default omit
        """;

    /// <summary>Creates the root command with its serve, prepare, and clean subcommands.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getEnvironmentVariable"/>, <paramref name="resolveExecutable"/>, or <paramref name="runProcess"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(
        Func<string, string?> getEnvironmentVariable,
        Func<string, string?> resolveExecutable,
        Func<ProcessStartInfo, TextWriter?, CancellationToken, Task<ProcessResult>> runProcess)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        ArgumentNullException.ThrowIfNull(runProcess);
        var application = new PlaywrightMcpApplication(getEnvironmentVariable, resolveExecutable, runProcess);

        var serve = new Command("serve", "Run the pinned Playwright MCP image as a stdio MCP server");
        serve.SetAction((parseResult, ct) => application.Serve(parseResult.InvocationConfiguration.Error, ct));

        var prepare = new Command("prepare", "Pull the pinned Playwright MCP image into the local container engine");
        prepare.SetAction((parseResult, ct) => application.Prepare(parseResult.InvocationConfiguration.Error, ct));

        var clean = new Command("clean", "Remove the pinned Playwright MCP image from the local container engine");
        clean.SetAction((parseResult, ct) => application.Clean(parseResult.InvocationConfiguration.Error, ct));

        var root = new RootCommand(Description);

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(serve);
        root.Subcommands.Add(prepare);
        root.Subcommands.Add(clean);
        return root;
    }
}
