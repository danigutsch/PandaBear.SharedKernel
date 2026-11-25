using System.Net.Http.Headers;
using System.Reflection;
using SharedKernel.Processes;

namespace SharedKernel.CI.Tool;

/// <summary>What the commands use to reach processes, the network, the environment, and the clock.</summary>
/// <param name="Machine">Runs Git, dotnet, and the other child processes, starts services, and tells time.</param>
/// <param name="CreateDownloadHandler">Creates the HTTP handler of one <c>stage-candidate</c> run, which disposes it.</param>
/// <param name="DownloadLimits">Bounds what <c>stage-candidate --repository</c> downloads.</param>
/// <param name="CreateApiHandler">Creates the HTTP handler of one <c>prune-prereleases</c> run, which disposes it.</param>
/// <param name="GetEnvironmentVariable">Reads an environment variable, such as <c>GH_TOKEN</c>, or returns <see langword="null"/>.</param>
/// <param name="DownloadTimeProvider">Times the downloads of <c>stage-candidate</c>, or <see langword="null"/> for the machine's clock.</param>
internal sealed record CiToolServices(
    CiMachine Machine,
    Func<HttpMessageHandler> CreateDownloadHandler,
    CandidateDownloadLimits DownloadLimits,
    Func<HttpMessageHandler> CreateApiHandler,
    Func<string, string?> GetEnvironmentVariable,
    TimeProvider? DownloadTimeProvider = null)
{
    /// <summary>Gets the user agent of every request: <c>sharedkernel-ci/&lt;version&gt;</c>.</summary>
    public static ProductInfoHeaderValue UserAgent { get; } = new(
        "sharedkernel-ci",
        typeof(CiToolServices).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0");

    /// <summary>
    /// Creates the services of the real tool: processes from the absolute <c>PATH</c> directories, HTTPS without cookies or
    /// automatic redirects, the process environment, and the system clock.
    /// </summary>
    public static CiToolServices CreateDefault() => new(
        new CiMachine(new SystemProcessRunner(), PathExecutableResolver.Default, TimeProvider.System),
        CreateHandler,
        CandidateDownloadLimits.Default,
        CreateHandler,
        Environment.GetEnvironmentVariable);

    // Downloads follow redirects themselves, each only to HTTPS; API requests refuse them, so the token never reaches another address.
    private static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false, UseCookies = false };
}
