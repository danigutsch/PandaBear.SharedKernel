using System.CommandLine;
using System.ComponentModel;
using PandaBear.Tooling;

namespace PandaBear.K6.Tool;

/// <summary>Builds the <c>sharedkernel-k6</c> command tree.</summary>
internal static class K6CommandLine
{
    /// <summary>Exit code for invalid configuration, a rejected argument or target, or a failed host or runner start.</summary>
    private const int FailureExitCode = 2;

    /// <summary>Creates the command tree.</summary>
    /// <param name="workingDirectory">Directory that a relative <c>--root</c> resolves from.</param>
    /// <param name="getEnvironmentVariable">Reads the variables that configure the target and reach k6.</param>
    /// <param name="resolveExecutable">Resolves a command name to an absolute executable path, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="getEnvironmentVariable"/> or <paramref name="resolveExecutable"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(
        string workingDirectory,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string?> resolveExecutable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(resolveExecutable);

        var scenarioOption = new Option<string>("--scenario")
        {
            Description = "Scenario of .config/sharedkernel-k6.json to run.",
            HelpName = "name",
            Required = true,
        };
        var containerOption = new Option<bool>("--container")
        {
            Description = "Run k6 in the configured container engine and image instead of a local k6.",
        };
        var imageOption = new Option<string>("--image")
        {
            Description = "k6 image, pinned by digest, that replaces the configured image. Requires --container.",
            HelpName = "ref@sha256:digest",
        };
        imageOption.Validators.Add(static result =>
        {
            if (!K6ConfigurationFile.IsDigestPinned(result.GetValueOrDefault<string>() ?? string.Empty))
            {
                result.AddError("--image requires an image reference pinned by digest, such as grafana/k6:<tag>@sha256:<digest>.");
            }
        });
        var allowExternalOption = new Option<bool>("--allow-external-target")
        {
            Description = "Allow a target other than a loopback host.",
        };
        var allowHttpDebugOption = new Option<bool>("--allow-http-debug")
        {
            Description = "Allow k6 --http-debug, which can print request and response data.",
        };
        var allowRemoteOutputOption = new Option<bool>("--allow-remote-output")
        {
            Description = "Allow k6 --out, --log-output, --console-output, and --traces-output.",
        };
        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root. Defaults to the current directory.",
            HelpName = "path",
        };

        // An empty path resolves to the current directory, which a missing shell variable should not select.
        rootOption.Validators.Add(static result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError("--root requires a path.");
            }
        });
        var k6ArgumentsArgument = new Argument<string[]>("k6-arguments")
        {
            Description = "Arguments for k6 run, after --. The tool sets the summary export, the target, and the scenario environment.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        var run = new Command("run", "Run a configured k6 scenario against a loopback target and write its results under the configured results directory.")
        {
            scenarioOption,
            containerOption,
            imageOption,
            allowExternalOption,
            allowHttpDebugOption,
            allowRemoteOutputOption,
            rootOption,
            k6ArgumentsArgument,
        };
        run.Validators.Add(result =>
        {
            if (result.GetResult(imageOption) is not null && !result.GetValue(containerOption))
            {
                result.AddError("--image requires --container.");
            }
        });
        run.SetAction((parseResult, ct) => Execute(
            parseResult,
            error => K6Run.Run(
                new K6RunOptions(
                    Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory),
                    parseResult.GetRequiredValue(scenarioOption),
                    parseResult.GetValue(containerOption),
                    parseResult.GetValue(imageOption),
                    parseResult.GetValue(allowExternalOption),
                    parseResult.GetValue(allowHttpDebugOption),
                    parseResult.GetValue(allowRemoteOutputOption),
                    parseResult.GetValue(k6ArgumentsArgument) ?? []),
                getEnvironmentVariable,
                resolveExecutable,
                error,
                ct),
            ct));

        var root = new RootCommand(
            "Runs the k6 scenarios that .config/sharedkernel-k6.json configures, locally or in a digest-pinned container, with a minimal k6 environment and loopback targets.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(run);
        return root;
    }

    private static async Task<int> Execute(ParseResult parseResult, Func<TextWriter, Task<int>> action, CancellationToken ct)
    {
        var error = parseResult.InvocationConfiguration.Error;
        try
        {
            return await action(error).ConfigureAwait(false);
        }
        catch (Exception exception) when (ct.IsCancellationRequested)
        {
            // Ctrl+C and SIGTERM sent to the process group also stop k6 and the host, which can fail before the token
            // is observed. System.CommandLine exits 1 for a cancellation.
            throw new OperationCanceledException("The command was canceled.", exception, ct);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or HttpRequestException
            or Win32Exception)
        {
            // Messages carry environment values and host output, so each line is escaped on its own.
            foreach (var line in exception.Message.TrimEnd('\r', '\n').Split('\n'))
            {
                await error.WriteLineAsync(ControlCharacters.Escape(line.TrimEnd('\r'))).ConfigureAwait(false);
            }

            return FailureExitCode;
        }
    }
}
