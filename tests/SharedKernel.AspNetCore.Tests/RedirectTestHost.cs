using Microsoft.AspNetCore.Builder;

namespace SharedKernel.AspNetCore.Tests;

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

    /// <summary>
    /// Starts a host that looks up the canonical form of each request path (see <see cref="PermanentRedirectPaths.Normalize"/>) among
    /// stored moves and redirects to the encoded target; every other path is answered with 200.
    /// </summary>
    /// <param name="moved">The canonical target of each canonical old path.</param>
    /// <returns>The host.</returns>
    internal static Task<PipelineTestHost> StartStored(IReadOnlyDictionary<string, string> moved) =>
        PipelineTestHost.Start(app =>
        {
            app.UsePermanentRedirects(context => ValueTask.FromResult(
                PermanentRedirectPaths.Normalize(context.Request.Path.Value, 512) is { } canonical && moved.TryGetValue(canonical, out var target)
                    ? new PermanentRedirect(PermanentRedirectPaths.Encode(target))
                    : null));
            app.MapFallback(() => "ok");
        });
}
