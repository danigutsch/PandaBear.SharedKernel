namespace PandaBear.CI.Tool;

/// <summary>Bounds what <c>stage-candidate --repository</c> downloads and how long it waits.</summary>
/// <param name="MaxPackageBytes">The largest package; the largest package of a recent candidate is about 10 MiB.</param>
/// <param name="MaxTotalBytes">The most package bytes one run downloads; a recent candidate's packages total about 18 MiB.</param>
/// <param name="RequestTimeout">How long one request, including its redirects and body, may take.</param>
/// <param name="MaxAttempts">How many times a request is sent when the server answers 408, 429, or 5xx.</param>
/// <param name="MaxRetryDelay">The longest wait before a retry, whatever <c>Retry-After</c> asks for.</param>
internal sealed record CandidateDownloadLimits(
    long MaxPackageBytes,
    long MaxTotalBytes,
    TimeSpan RequestTimeout,
    int MaxAttempts,
    TimeSpan MaxRetryDelay)
{
    /// <summary>Gets the limits that the tool uses.</summary>
    public static CandidateDownloadLimits Default
    {
        get;
    } = new(
        64L * 1024 * 1024,
        256L * 1024 * 1024,
        TimeSpan.FromMinutes(5),
        4,
        TimeSpan.FromSeconds(60));
}
