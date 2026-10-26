using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds a k6 scenario to an Aspire AppHost model as an explicit-start container.
/// </summary>
public static class K6ScenarioResourceExtensions
{
    private const string ImageTag = "0.49.0";
    private const string ImageDigest = "8cd78f9d0de5f50bc8821cceecf356d5d9e839e6611c226a3fcf13c591080fbd";
    private const string ScriptsTarget = "/scripts";
    private const string LibraryTarget = "/k6lib";
    private const string ResultsTarget = "/results";
    private const string LogName = "k6.log";
    private const string ConsoleName = "k6-console.log";

    /// <summary>
    /// Adds a k6 container that runs one script and starts only when requested from the dashboard or the Aspire CLI.
    /// </summary>
    /// <remarks>
    /// The image is pinned by tag and digest. The script directory and the helper library (<c>/k6lib/auth.js</c>
    /// and <c>/k6lib/summary.js</c>) mount read-only; the results directory mounts writable at <c>/results</c>.
    /// k6 writes its own log (<c>k6.log</c>, for example a failed token request) and the script's <c>console</c> output
    /// (<c>k6-console.log</c>) to files in the results directory, so a run can be read after the container exits
    /// without a container log driver. Both files are emptied when this method runs and appended to by each run; treat
    /// them like the summary file, because they are part of the run results.
    /// On Linux and macOS a results directory that this call creates is made writable for the container user with the
    /// sticky bit set; an existing results directory keeps its mode and must already allow the container user to write.
    /// </remarks>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="scriptDirectory">The host directory that holds the k6 scripts.</param>
    /// <param name="script">The script path under <paramref name="scriptDirectory"/>, with <c>/</c> separators.</param>
    /// <param name="resultsDirectory">The host directory that receives the scenario results.</param>
    /// <returns>The configured k6 container resource.</returns>
    public static IResourceBuilder<K6Resource> AddK6Scenario(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string scriptDirectory,
        string script,
        string resultsDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultsDirectory);

        var scripts = Path.GetFullPath(scriptDirectory);
        var scriptPath = Path.GetFullPath(Path.Combine(scripts, script));
        if (!scriptPath.StartsWith(scripts + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The script must be under the script directory.", nameof(script));
        }

        var results = Path.GetFullPath(resultsDirectory);
        var created = !Directory.Exists(results);
        Directory.CreateDirectory(results);
        if (created && !OperatingSystem.IsWindows())
        {
            // The k6 image runs as an unprivileged user that is not the host user. The sticky bit keeps that user
            // from deleting or replacing files it does not own.
            File.SetUnixFileMode(results, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
                | UnixFileMode.StickyBit);
        }

        PrepareLogFile(Path.Combine(results, LogName));
        PrepareLogFile(Path.Combine(results, ConsoleName));

        return builder.AddK6(name)
            .WithImageTag(ImageTag)
            .WithImageSHA256(ImageDigest)
            .WithBindMount(scripts, ScriptsTarget, isReadOnly: true)
            .WithBindMount(K6Library.Materialize(), LibraryTarget, isReadOnly: true)
            .WithBindMount(results, ResultsTarget)
            .WithScript(ScriptsTarget + "/" + script.Replace('\\', '/'))
            .WithArgs("--log-output=file=" + ResultsTarget + "/" + LogName, "--console-output=" + ResultsTarget + "/" + ConsoleName)
            .WithExplicitStart();
    }

    /// <summary>
    /// Passes the client-credentials settings that <c>/k6lib/auth.js</c> reads and waits for the identity provider.
    /// </summary>
    /// <remarks>
    /// The environment names are <c>SK_K6_TOKEN_URL</c>, <c>SK_K6_CLIENT_ID</c>, <c>SK_K6_CLIENT_SECRET</c>, and
    /// <c>SK_K6_TOKEN_HOST</c>. The secret is an Aspire secret parameter. The token request goes to the identity
    /// provider through the container network, and carries the <c>Host</c> of its host-side endpoint so the issuer of the
    /// token matches the issuer that services reach through <c>localhost</c>.
    /// </remarks>
    /// <param name="k6">The k6 container resource.</param>
    /// <param name="identityProvider">The identity provider; its <c>http</c> endpoint hosts the token endpoint.</param>
    /// <param name="tokenPath">The token endpoint path on the identity provider, starting with <c>/</c>.</param>
    /// <param name="clientId">The client id of the confidential service-account client.</param>
    /// <param name="clientSecret">The secret parameter that holds the client secret.</param>
    /// <returns>The configured k6 container resource.</returns>
    public static IResourceBuilder<K6Resource> WithK6ClientCredentials(
        this IResourceBuilder<K6Resource> k6,
        IResourceBuilder<IResourceWithEndpoints> identityProvider,
        string tokenPath,
        string clientId,
        IResourceBuilder<ParameterResource> clientSecret)
    {
        ArgumentNullException.ThrowIfNull(k6);
        ArgumentNullException.ThrowIfNull(identityProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);

        var endpoint = identityProvider.GetEndpoint("http");
        var hostEndpoint = identityProvider.GetEndpoint("http", KnownNetworkIdentifiers.LocalhostNetwork);
        return k6
            .WithEnvironment("SK_K6_TOKEN_URL", ReferenceExpression.Create($"{endpoint}{tokenPath}"))
            .WithEnvironment("SK_K6_TOKEN_HOST", hostEndpoint.Property(EndpointProperty.HostAndPort))
            .WithEnvironment("SK_K6_CLIENT_ID", clientId)
            .WithEnvironment("SK_K6_CLIENT_SECRET", clientSecret)
            .WaitFor(identityProvider);
    }

    /// <summary>
    /// Empties a log file and makes it shared. k6 creates its own log file readable by the container user only, but it
    /// keeps the mode of a file that exists, so the host user can read what the container appends.
    /// </summary>
    private static void PrepareLogFile(string path)
    {
        // A link is replaced instead of written through.
        if (new FileInfo(path).LinkTarget is not null)
        {
            File.Delete(path);
        }

        File.WriteAllBytes(path, []);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
        }
    }
}
