using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Answers <see cref="WebsiteId"/> for every path except <see cref="UntrackedPath"/>, which has no website, and <see cref="FailingPath"/>, where the lookup fails.</summary>
internal sealed class FixedWebsiteIdProvider : IUmamiWebsiteIdProvider
{
    /// <summary>The website id of every tracked page.</summary>
    internal static readonly Guid WebsiteId = Guid.Parse("8f1d8a3e-7c2b-4d5e-9f60-1a2b3c4d5e6f");

    /// <summary>A path with no website id.</summary>
    internal const string UntrackedPath = "/untracked";

    /// <summary>A path whose lookup throws.</summary>
    internal const string FailingPath = "/lookup-fails";

    /// <summary>The message of the lookup failure, which must never reach a log.</summary>
    internal const string FailureMessage = "lookup-secret";

    /// <inheritdoc />
    public ValueTask<Guid?> GetWebsiteId(HttpContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Path.Value switch
        {
            UntrackedPath => ValueTask.FromResult<Guid?>(null),
            FailingPath => throw new InvalidOperationException(FailureMessage),
            _ => ValueTask.FromResult<Guid?>(WebsiteId),
        };
    }
}
