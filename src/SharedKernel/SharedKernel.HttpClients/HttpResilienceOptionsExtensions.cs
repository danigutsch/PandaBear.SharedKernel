using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace PandaBear.HttpClients;

/// <summary>Customizations of the standard HTTP resilience pipeline.</summary>
public static class HttpResilienceOptionsExtensions
{
    /// <summary>
    /// Makes a 429 Too Many Requests answer final: it is not retried, which would spend more of a quota the server just reported exhausted,
    /// and not counted by the circuit breaker, which would otherwise cut off every request to that server after a burst of quota answers.
    /// Every other outcome keeps the predicates already configured. Call it where the standard resilience options are configured, such as
    /// the <c>ConfigureResilience</c> callback of the SharedKernel service defaults.
    /// </summary>
    /// <param name="options">The standard resilience options.</param>
    /// <param name="appliesTo">
    /// Limits the rule to the requests it accepts, such as the routes of one quota-limited API; <see langword="null"/> applies it to every
    /// request. A 429 whose request is unknown or not accepted keeps the configured behavior.
    /// </param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static HttpStandardResilienceOptions DisableRetryOnTooManyRequests(
        this HttpStandardResilienceOptions options,
        Func<HttpRequestMessage, bool>? appliesTo = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var retry = options.Retry.ShouldHandle;
        options.Retry.ShouldHandle = arguments =>
            IsFinal(arguments.Context, arguments.Outcome, appliesTo) ? ValueTask.FromResult(false) : retry(arguments);

        var circuitBreaker = options.CircuitBreaker.ShouldHandle;
        options.CircuitBreaker.ShouldHandle = arguments =>
            IsFinal(arguments.Context, arguments.Outcome, appliesTo) ? ValueTask.FromResult(false) : circuitBreaker(arguments);

        return options;
    }

    private static bool IsFinal(ResilienceContext context, Outcome<HttpResponseMessage> outcome, Func<HttpRequestMessage, bool>? appliesTo)
    {
        if (outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return false;
        }

        return appliesTo is null || (context.GetRequestMessage() is { } request && appliesTo(request));
    }
}
