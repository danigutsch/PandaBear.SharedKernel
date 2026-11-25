using System.CommandLine;

namespace SharedKernel.PlaywrightMcp.Tool;

/// <summary>Builds the command-line interface of the Playwright MCP tool.</summary>
internal static class PlaywrightMcpCommandLine
{
    private static readonly string Description = $"""
        Run the pinned Playwright MCP image on local Docker or rootless Podman.
        Image: {PlaywrightMcpServer.Image}

        Environment variables:
          PLAYWRIGHT_MCP_ENGINE           docker or podman; required when both are installed
          PLAYWRIGHT_MCP_NETWORK_ACCESS   1 enables container networking; default 0
          PLAYWRIGHT_MCP_IMAGE_RESPONSES  allow sends images to the MCP client; default omit
        """;

    /// <summary>
    /// Creates the root command with its serve, prepare, and clean subcommands. Each action reads the settings from the
    /// environment when it runs and passes them to <paramref name="server"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="server"/> or <paramref name="getEnvironmentVariable"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(PlaywrightMcpServer server, Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        PlaywrightMcpSettings Settings() => PlaywrightMcpSettings.FromEnvironment(getEnvironmentVariable);

        var serve = new Command("serve", "Run the pinned Playwright MCP image as a stdio MCP server");
        serve.SetAction((parseResult, ct) => server.Serve(Settings(), parseResult.InvocationConfiguration.Error, ct));

        var prepare = new Command("prepare", "Pull the pinned Playwright MCP image into the local container engine");
        prepare.SetAction((parseResult, ct) => server.Prepare(Settings(), parseResult.InvocationConfiguration.Error, ct));

        var clean = new Command("clean", "Remove the pinned Playwright MCP image from the local container engine");
        clean.SetAction((parseResult, ct) => server.Clean(Settings(), parseResult.InvocationConfiguration.Error, ct));

        var root = new RootCommand(Description);

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(serve);
        root.Subcommands.Add(prepare);
        root.Subcommands.Add(clean);
        return root;
    }
}
