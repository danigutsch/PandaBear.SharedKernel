using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Serves synthetic NuGet Audit V3 metadata from a loopback address.</summary>
internal sealed class NuGetTrialAuditFeed : IAsyncDisposable
{
    /// <summary>The advisory URI used by the test feed.</summary>
    internal const string Advisory = "https://example.invalid/advisories/trial";

    /// <summary>The advisory suppressed by the solution projects but reported by the standalone file app.</summary>
    internal const string FileAppAdvisory = "https://example.invalid/advisories/file-app";

    private readonly HttpListener listener = new();
    private readonly Task serving;
    private readonly string index;
    private readonly string pages;
    private readonly string advisories;
    private readonly string unmappedIndex;
    private int unmappedRequests;

    /// <summary>Starts a source for the given synthetic package and affected version range.</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "The NuGet VulnerabilityInfo protocol requires lowercase package IDs.")]
    internal NuGetTrialAuditFeed(string affectedPackage, string versionRange, bool available = true, bool fileAppAdvisory = false)
    {
        var address = $"http://127.0.0.1:{LoopbackPort.GetFree()}/";
        IndexUri = new Uri(address + "index.json");
        UnmappedUri = new Uri(address + "unmapped/index.json");
        unmappedIndex = $$"""{"version":"3.0.0","resources":[{"@id":"{{address}}unmapped/flat/","@type":"PackageBaseAddress/3.0.0"}]}""";
        index = available
            ? $$"""{"version":"3.0.0","resources":[{"@id":"{{address}}vulnerabilities.json","@type":"VulnerabilityInfo/6.7.0"}]}"""
            : """{"version":"3.0.0","resources":[]}""";
        pages = $$"""[{"@name":"base","@id":"{{address}}base.json","@updated":"2026-01-01T00:00:00Z"}]""";
        var fileAppEntry = fileAppAdvisory
            ? $$""","{{NuGetTrialRepository.Driver.ToLowerInvariant()}}":[{"severity":2,"url":"{{FileAppAdvisory}}","versions":"[2.0.0]"}]"""
            : string.Empty;
        advisories = $$"""{"{{affectedPackage.ToLowerInvariant()}}":[{"severity":2,"url":"{{Advisory}}","versions":"{{versionRange}}"}]{{fileAppEntry}}}""";
        listener.Prefixes.Add(address);
        listener.Start();
        serving = Serve();
    }

    /// <summary>The configured feed service-index URI.</summary>
    internal Uri IndexUri { get; }

    /// <summary>A configured package source that must not receive requests for the mapped fixture.</summary>
    internal Uri UnmappedUri { get; }

    /// <summary>Number of requests received by the excluded package source.</summary>
    internal int UnmappedRequests => Volatile.Read(ref unmappedRequests);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        listener.Close();
        await serving.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext request;
            try
            {
                request = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            using var response = request.Response;
            var path = request.Request.Url?.AbsolutePath ?? string.Empty;
            if (path.StartsWith("/unmapped/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref unmappedRequests);
            }

            var payload = path switch
            {
                "/index.json" => index,
                "/vulnerabilities.json" => pages,
                "/base.json" => advisories,
                "/unmapped/index.json" => unmappedIndex,
                _ when path.StartsWith("/unmapped/flat/", StringComparison.Ordinal) => """{"versions":["1.0.1"]}""",
                _ => null,
            };
            if (payload is null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                continue;
            }

            var body = Encoding.UTF8.GetBytes(payload);
            response.ContentType = "application/json";
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body);
        }
    }
}
