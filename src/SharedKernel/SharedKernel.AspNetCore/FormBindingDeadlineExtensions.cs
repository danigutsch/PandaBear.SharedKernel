// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http.Timeouts;

namespace SharedKernel.AspNetCore;

/// <summary>Applies ASP.NET Core request deadlines to implicit form binding.</summary>
public static class FormBindingDeadlineExtensions
{
    /// <summary>
    /// Forwards the active request deadline to body reads for endpoints with <see cref="IFormOptionsMetadata"/>.
    /// Register it after routing and <c>UseRequestTimeouts</c>, before the endpoints read the request body.
    /// </summary>
    /// <remarks>
    /// Implicit form binding can read without a cancellation token. Applying cancellation at the stream
    /// lets the request timeout middleware write its configured timeout response.
    /// The application owns timeout registration, endpoint form limits, and timeout policy.
    /// </remarks>
    /// <param name="app">The application's middleware pipeline.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseFormBindingDeadline(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            if (context.Features.Get<IHttpRequestTimeoutFeature>() is null
                || context.GetEndpoint()?.Metadata.GetMetadata<IFormOptionsMetadata>() is null)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var body = context.Request.Body;
            using var deadlineBody = new DeadlineRequestBody(body, context.RequestAborted);
            context.Request.Body = deadlineBody;
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                context.Request.Body = body;
            }
        });
    }
}
