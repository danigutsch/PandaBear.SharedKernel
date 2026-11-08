using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PandaBear.AspNetCore.Authentication;

/// <summary>Wraps the JWT bearer events of every scheme so failures, challenges and forbidden results are reported before the existing handlers run.</summary>
internal sealed class JwtBearerSecurityEventsPostConfigureOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JwtBearerEvents, object> Wrapped = new();

    /// <summary>An events type resolved from dependency injection replaces the instance, so that choice is left to the host.</summary>
    private static bool HostResolvesEventsFromDependencyInjection(JwtBearerOptions options) => options.EventsType is not null;

    /// <summary>A host may share one events instance across schemes, so each instance is wrapped only once.</summary>
    private static bool FirstWrap(JwtBearerEvents events) => Wrapped.TryAdd(events, true);

    /// <inheritdoc />
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (HostResolvesEventsFromDependencyInjection(options))
        {
            return;
        }

        var events = options.Events ??= new JwtBearerEvents();

        if (!FirstWrap(events))
        {
            return;
        }

        var failed = events.OnAuthenticationFailed;
        var challenge = events.OnChallenge;
        var forbidden = events.OnForbidden;

        events.OnAuthenticationFailed = context =>
        {
            Report(context.HttpContext, AuthenticationEventNames.AuthenticationFailed, Reason(context.Exception), context.Scheme.Name);
            return failed(context);
        };
        events.OnChallenge = context =>
        {
            Report(context.HttpContext, AuthenticationEventNames.Challenge, context.AuthenticateFailure is null ? AuthenticationEventNames.MissingToken : Reason(context.AuthenticateFailure), context.Scheme.Name);
            return challenge(context);
        };
        events.OnForbidden = context =>
        {
            Report(context.HttpContext, AuthenticationEventNames.Forbidden, "insufficient_permission", context.Scheme.Name);
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
