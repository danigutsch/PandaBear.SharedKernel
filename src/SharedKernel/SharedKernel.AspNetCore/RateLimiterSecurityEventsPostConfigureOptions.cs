using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore;

/// <summary>Wraps <see cref="RateLimiterOptions.OnRejected" /> so each rejection is reported before the existing callback runs.</summary>
internal sealed class RateLimiterSecurityEventsPostConfigureOptions : IPostConfigureOptions<RateLimiterOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inner = options.OnRejected;
        options.OnRejected = (context, cancellationToken) =>
        {
            var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                ?? AspNetCoreTelemetry.NoPolicy;
            context.HttpContext.RequestServices.GetService<SecurityEventReporter>()
                ?.ReportRateLimitRejection(context.HttpContext, policy);
            return inner is null ? ValueTask.CompletedTask : inner(context, cancellationToken);
        };
    }
}
