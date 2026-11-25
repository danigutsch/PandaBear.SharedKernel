using System.Net.Http.Headers;

namespace SharedKernel.CI;

/// <summary>
/// Verifies a pinned package candidate against a clean library checkout, then stages its packages from a forge release or
/// a directory in a local feed.
/// </summary>
public sealed class CandidateStager
{
    private readonly CiMachine _machine;
    private readonly Func<HttpMessageHandler> _createHttpHandler;
    private readonly ProductInfoHeaderValue _userAgent;

    /// <summary>Creates the stager.</summary>
    /// <param name="machine">Runs Git; its clock also times the downloads unless <see cref="DownloadTimeProvider"/> is set.</param>
    /// <param name="createHttpHandler">
    /// Creates the handler of one staging run, which disposes it. It should not follow redirects itself: the download
    /// follows at most five, each only to HTTPS.
    /// </param>
    /// <param name="userAgent">Names the caller in every download request, such as <c>sharedkernel-ci/1.0</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CandidateStager(CiMachine machine, Func<HttpMessageHandler> createHttpHandler, ProductInfoHeaderValue userAgent)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(createHttpHandler);
        ArgumentNullException.ThrowIfNull(userAgent);
        _machine = machine;
        _createHttpHandler = createHttpHandler;
        _userAgent = userAgent;
    }

    /// <summary>Gets the bounds of the downloads; <see cref="CandidateDownloadLimits.Default"/> unless set.</summary>
    public CandidateDownloadLimits Limits { get; init; } = CandidateDownloadLimits.Default;

    /// <summary>Gets the clock that times download requests and retries and ages staging directories; the machine's clock unless set.</summary>
    public TimeProvider? DownloadTimeProvider { get; init; }

    /// <summary>
    /// Validates the pin, checks the SDK lockstep and the library checkout, then stages every missing package and writes the
    /// pin as <c>candidate.json</c>. Packages are verified against their pinned SHA-256 hashes before they reach the output,
    /// and a different package already there is never overwritten.
    /// </summary>
    /// <param name="request">The pin, the checkout, the output, and the feed directory when no release supplies the packages.</param>
    /// <param name="release">The release assets of the forge repository whose <c>v&lt;version&gt;</c> release supplies the packages, or <see langword="null"/> to copy them from <see cref="CandidateStagingRequest.Feed"/>.</param>
    /// <param name="output">Receives the result line.</param>
    /// <param name="ct">Cancels Git and the downloads.</param>
    /// <exception cref="ArgumentException">Both or neither of <paramref name="release"/> and the feed are given.</exception>
    /// <exception cref="InvalidOperationException">The pin, the checkout, or the output is refused.</exception>
    /// <exception cref="InvalidDataException">A package or the release manifest does not match the pin.</exception>
    /// <exception cref="HttpRequestException">A download failed.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task Stage(CandidateStagingRequest request, IReleaseAssets? release, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);
        if ((release is null) == (request.Feed is null))
        {
            throw new ArgumentException("Stage from either a release or a feed directory.", nameof(release));
        }

        var services = new CandidateStagingServices(_machine.RunGit, _createHttpHandler, DownloadTimeProvider ?? _machine.TimeProvider, Limits, _userAgent);
        return CandidateStaging.Run(request, release, services, output, ct);
    }
}
