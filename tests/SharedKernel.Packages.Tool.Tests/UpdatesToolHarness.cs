using System.CommandLine;
using System.Globalization;
using PandaBear.Results;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Runs the <c>updates</c> command line with fake tools and fake sources.</summary>
internal static class UpdatesToolHarness
{
    /// <summary>The time that the fake sources decide at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The cargo output that lists bat 0.24.0, ripgrep 14.1.0 and fd 9.0.0.</summary>
    public const string CargoList = "bat v0.24.0:\n    bat\nripgrep v14.1.0:\n    rg\nfd-find v9.0.0:\n    fd\n";

    /// <summary>Runs <c>updates</c> with <paramref name="arguments"/> against <paramref name="root"/>.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(
        string root,
        IProcessRunner runner,
        Func<string, string?> resolve,
        Func<UpdateSources> createSources,
        params string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await PackagesCommandLine
            .Create(root, resolve, runner, createSources)
            .Parse(["updates", "--root", root, .. arguments])
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>An advisory lookup that found nothing.</summary>
    public static readonly Result<IReadOnlyList<Advisory>> NoAdvisories = Result.Ok<IReadOnlyList<Advisory>>([]);

    /// <summary>Fake sources for bat, fd-find, and corepack.</summary>
    public static FakeUpdateSources Sources(Result<IReadOnlyList<Advisory>>? advisories = null) => new(
        new()
        {
            ["bat"] = "0.26.1",
            ["fd-find"] = "10.0.0",
            ["corepack"] = "0.40.0"
        },
        new()
        {
            ["bat"] = Now.AddDays(-30),
            ["fd-find"] = Now.AddDays(-2),
            ["corepack"] = Now.AddDays(-30)
        },
        advisories ?? NoAdvisories,
        Now);

    /// <summary>A runner that answers cargo and npm.</summary>
    public static RoutingProcessRunner Runner() => new(new Dictionary<string, ProcessOutput>
    {
        ["/fake/cargo"] = new(0, CargoList),
        ["/fake/npm"] = new(0, """{"dependencies":{"corepack":{"version":"0.35.0"}}}"""),
    });
}
