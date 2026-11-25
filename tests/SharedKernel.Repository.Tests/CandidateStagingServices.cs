namespace SharedKernel.Repository.Tests;

/// <summary>What a test gives <c>stage-candidate</c> to reach Git, the network, and the clock.</summary>
/// <param name="Git">Answers each Git command of the run.</param>
/// <param name="CreateHttpHandler">Creates the HTTP handler of the run.</param>
/// <param name="TimeProvider">Times requests and retries, and ages staging directories.</param>
/// <param name="Limits">Bounds downloads.</param>
internal sealed record CandidateStagingServices(
    Func<string[], string, CancellationToken, Task<string>> Git,
    Func<HttpMessageHandler> CreateHttpHandler,
    TimeProvider TimeProvider,
    CandidateDownloadLimits Limits);
