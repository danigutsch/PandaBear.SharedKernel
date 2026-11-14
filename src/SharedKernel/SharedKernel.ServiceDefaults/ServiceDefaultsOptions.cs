using Microsoft.Extensions.Http.Resilience;

namespace PandaBear.ServiceDefaults;

/// <summary>Settings of <see cref="ServiceDefaultsExtensions.AddSharedKernelServiceDefaults{TBuilder}"/>.</summary>
public sealed class ServiceDefaultsOptions
{
    /// <summary>Gets or sets the path of the readiness endpoint, which runs every health check. The default is <c>/health</c>.</summary>
    public string HealthPath { get; set; } = "/health";

    /// <summary>
    /// Gets or sets the path of the liveness endpoint, which runs only the checks tagged <see cref="ServiceDefaultsExtensions.LiveTag"/>.
    /// The default is <c>/alive</c>.
    /// </summary>
    public string AlivePath { get; set; } = "/alive";

    /// <summary>Gets or sets when the health endpoints are mapped. The default is <see cref="HealthEndpointExposure.Always"/>.</summary>
    public HealthEndpointExposure Exposure { get; set; } = HealthEndpointExposure.Always;

    /// <summary>
    /// Gets or sets a value indicating whether every <see cref="HttpClient"/> gets the standard resilience handler (retries, circuit breaker,
    /// timeouts). The default is <see langword="true"/>.
    /// </summary>
    public bool AddResilience { get; set; } = true;

    /// <summary>
    /// Gets or sets a change to the standard resilience options shared by every <see cref="HttpClient"/>, such as fewer retries or a
    /// predicate that leaves some answers alone; <see langword="null"/> keeps the standard options. Retries of unsafe HTTP methods stay
    /// off after it runs, even when it replaces the retry predicate. It is not used when <see cref="AddResilience"/> is off.
    /// </summary>
    public Action<HttpStandardResilienceOptions>? ConfigureResilience { get; set; }

    /// <summary>
    /// Gets the host-owned telemetry attribute names, compared without case, that the privacy processors remove on top of the shared
    /// SharedKernel baseline. They can only add names; they cannot permit a name the baseline removes.
    /// </summary>
    public ISet<string> SensitiveTelemetryAttributes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
