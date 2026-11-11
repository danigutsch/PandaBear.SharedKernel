using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore;

/// <summary>Reports an unhandled exception and answers with a RFC 9457 problem body that carries no exception text.</summary>
/// <remarks>
/// The framework's exception middleware calls handlers only before the response starts (verified against
/// <c>ExceptionHandlerMiddlewareImpl</c> in dotnet/aspnetcore: once <c>Response.HasStarted</c> it logs and rethrows
/// without calling any handler), so this handler needs no started-response branch.
/// </remarks>
internal sealed class UnhandledExceptionHandler(UnhandledExceptionReporter reporter) : IExceptionHandler
{
    private const string TraceIdExtension = "traceId";

    /// <summary>A client that went away is not a server fault, so it is left to the framework.</summary>
    private static bool ClientWentAway(Exception exception, HttpContext httpContext) =>
        exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested;

    /// <inheritdoc />
    /// <remarks>
    /// A malformed or oversized request (Kestrel 400, 413, 431 ...) is a client error that carries its own status.
    /// It is answered with that status and is not reported as an unhandled server exception.
    /// The problem-details service is trimming-safe and honors the host's content negotiation. When it finds no writer
    /// for the request's <c>Accept</c> header (a browser that navigates to a failing URL sends only <c>text/html</c>), the
    /// same problem is written as <c>application/problem+json</c> after the host's customization, so no error leaves without a body.
    /// </remarks>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (ClientWentAway(exception, httpContext))
        {
            return false;
        }

        var statusCode = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = statusCode;
        if (exception is not BadHttpRequestException)
        {
            reporter.Report(httpContext, exception, handled: true);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode >= StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : "The request could not be processed.",
            Type = statusCode >= StatusCodes.Status500InternalServerError
                ? "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1"
                : "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1"
        };
        problem.Extensions[TraceIdExtension] = TelemetryHttpContext.TraceId(httpContext);

        var context = new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem };
        var written = await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .TryWriteAsync(context)
            .ConfigureAwait(false);
        if (!written)
        {
            await WriteProblemJson(context).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Writes the problem when no registered writer accepted the request: the service ran no customization, so it runs here.</summary>
    private static Task WriteProblemJson(ProblemDetailsContext context)
    {
        var services = context.HttpContext.RequestServices;
        services.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails?.Invoke(context);
        var serializer = services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        return context.HttpContext.Response.WriteAsJsonAsync(
            context.ProblemDetails, serializer.GetTypeInfo(typeof(ProblemDetails)), "application/problem+json");
    }
}
