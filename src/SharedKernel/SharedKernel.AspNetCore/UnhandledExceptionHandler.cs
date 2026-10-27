using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore;

/// <summary>Reports an unhandled exception and answers with a RFC 9457 problem body that carries no exception text.</summary>
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

        if (httpContext.Response.HasStarted)
        {
            reporter.Report(httpContext, exception, handled: false);
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        reporter.Report(httpContext, exception, handled: true);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1"
        };
        problem.Extensions[TraceIdExtension] = TelemetryHttpContext.TraceId(httpContext);

        // The problem-details service is trimming-safe and honors the host's content negotiation. A client that
        // accepts no problem format still receives the 500 status.
        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem })
            .ConfigureAwait(false);
        return true;
    }
}
