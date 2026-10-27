using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PandaBear.AspNetCore;

/// <summary>Wraps the JWT bearer events of every scheme so failures, challenges and forbidden results are reported before the existing handlers run.</summary>
internal sealed class JwtBearerSecurityEventsPostConfigureOptions : IPostConfigureOptions<JwtBearerOptions>
{
    /// <summary>The event tag value for a failed token validation.</summary>
    public const string AuthenticationFailed = "authentication_failed";
    /// <summary>The event tag value for a challenge response.</summary>
    public const string Challenge = "challenge";
    /// <summary>The event tag value for a forbidden response.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The reason for a challenge that carried no token.</summary>
    public const string MissingToken = "missing_token";

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JwtBearerEvents, object> Wrapped = new();

    /// <inheritdoc />
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // An events type resolved from DI replaces the instance; leave that choice to the host.
        if (options.EventsType is not null)
        {
            return;
        }

        var events = options.Events ??= new JwtBearerEvents();

        // A host may share one events instance across schemes; wrap it once.
        if (!Wrapped.TryAdd(events, true))
        {
            return;
        }

        var failed = events.OnAuthenticationFailed;
        var challenge = events.OnChallenge;
        var forbidden = events.OnForbidden;

        events.OnAuthenticationFailed = context =>
        {
            Report(context.HttpContext, AuthenticationFailed, Reason(context.Exception), context.Scheme.Name);
            return failed(context);
        };
        events.OnChallenge = context =>
        {
            Report(context.HttpContext, Challenge, context.AuthenticateFailure is null ? MissingToken : Reason(context.AuthenticateFailure), context.Scheme.Name);
            return challenge(context);
        };
        events.OnForbidden = context =>
        {
            Report(context.HttpContext, Forbidden, "insufficient_permission", context.Scheme.Name);
            return forbidden(context);
        };
    }

    /// <summary>Maps an exception to a fixed reason; the exception message is never read.</summary>
    internal static string Reason(Exception exception) => exception switch
    {
        SecurityTokenExpiredException => "token_expired",
        SecurityTokenNotYetValidException => "token_not_yet_valid",
        SecurityTokenSignatureKeyNotFoundException => "signing_key_not_found",
        SecurityTokenInvalidSignatureException => "invalid_signature",
        SecurityTokenInvalidAudienceException => "invalid_audience",
        SecurityTokenInvalidIssuerException => "invalid_issuer",
        SecurityTokenMalformedException => "malformed_token",
        SecurityTokenException => "invalid_token",
        _ => "authentication_error"
    };

    private static void Report(Microsoft.AspNetCore.Http.HttpContext httpContext, string securityEvent, string reason, string scheme) =>
        TelemetryGuard.Run(() => httpContext.RequestServices.GetService<SecurityEventReporter>()?.ReportAuthentication(httpContext, securityEvent, reason, scheme));
}
