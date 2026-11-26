using System.Globalization;
using System.Net;

namespace SharedKernel.Repositories.GitHub.Tests;

/// <summary>Queues synthetic GitHub Project membership pages.</summary>
internal static class GitHubProjectMembershipTestResponses
{
    /// <summary>Queues empty membership pages with distinct cursors and an advertised next page.</summary>
    public static void EnqueuePages(TestHttpMessageHandler handler, int count)
    {
        for (var page = 0; page < count; page++)
        {
            var cursor = page.ToString(CultureInfo.InvariantCulture);
            handler.EnqueueJson(HttpStatusCode.OK, $$"""
                { "data": { "node": { "items": {
                  "nodes": [], "pageInfo": { "hasNextPage": true, "endCursor": "{{cursor}}" }
                } } } }
                """);
        }
    }
}
