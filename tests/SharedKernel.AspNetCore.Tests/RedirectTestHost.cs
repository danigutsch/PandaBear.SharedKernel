using Microsoft.AspNetCore.Builder;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Starts hosts that redirect <c>/old</c> to a fixed answer and otherwise answer <c>/old</c> with 200.</summary>
internal static class RedirectTestHost
{
    /// <summary>Starts a host.</summary>
    /// <param name="target">What the resolver answers for <c>/old</c>.</param>
    /// <returns>The host.</returns>
    internal static Task<PipelineTestHost> Start(PermanentRedirect? target) =>
        PipelineTestHost.Start(app =>
        {
            app.UsePermanentRedirects(context => ValueTask.FromResult(context.Request.Path == "/old" ? target : null));
            app.MapGet("/old", () => "ok");
        });
}
