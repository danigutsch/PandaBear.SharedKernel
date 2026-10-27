using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // A client that went away is not a server fault; leave it to the framework.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        // A malformed or oversized request (Kestrel 400, 413, 431 ...) is a client error that carries its own status.
        // It is answered with that status and is not an unhandled server exception.
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

        // The problem-details service is trimming-safe and honors the host's content negotiation. A client that
        // accepts no problem format still receives the status.
        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem })
            .ConfigureAwait(false);
        return true;
    }
}
