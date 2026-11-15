using System.Net.Http.Headers;

namespace PandaBear.CI;

/// <summary>Supplies what candidate staging uses to reach Git, the network, and the clock.</summary>
/// <param name="Git">Runs Git in a directory and returns its standard output.</param>
/// <param name="CreateHttpHandler">Creates the HTTP handler for one run; the run disposes it.</param>
/// <param name="TimeProvider">Times requests and retries, and ages staging directories.</param>
/// <param name="Limits">Bounds downloads.</param>
/// <param name="UserAgent">Names the caller in every download request.</param>
internal sealed record CandidateStagingServices(
    Func<string[], string, CancellationToken, Task<string>> Git,
    Func<HttpMessageHandler> CreateHttpHandler,
    TimeProvider TimeProvider,
    CandidateDownloadLimits Limits,
    ProductInfoHeaderValue UserAgent);
