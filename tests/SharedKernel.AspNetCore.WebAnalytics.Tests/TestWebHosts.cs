using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Creates the test sites' builders.</summary>
internal static class TestWebHosts
{
    /// <summary>
    /// Creates a builder of a site on a test server that watches no configuration file, because one watcher per file and host exhausts
    /// the per-user inotify limit when many test hosts run in parallel.
    /// </summary>
    /// <returns>The builder.</returns>
    internal static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
        builder.WebHost.UseTestServer();
        return builder;
    }
}
