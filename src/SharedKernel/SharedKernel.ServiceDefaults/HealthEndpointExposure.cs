namespace SharedKernel.ServiceDefaults;

/// <summary>When <see cref="ServiceDefaultsExtensions.MapHealthEndpoints"/> maps the health endpoints.</summary>
public enum HealthEndpointExposure
{
    /// <summary>
    /// In every environment. Container orchestrators probe a production host, and an Aspire AppHost's <c>WithHttpHealthCheck</c> and
    /// <c>WaitFor</c> need the endpoint in whatever environment the project runs. The endpoints only return the overall status text.
    /// </summary>
    Always = 0,

    /// <summary>Only when the host environment is Development, as the Aspire service-defaults template does.</summary>
    DevelopmentOnly = 1,
}
