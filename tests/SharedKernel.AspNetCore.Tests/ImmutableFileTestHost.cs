using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// A loopback host that serves <c>/media/{fileName}</c> from a temporary folder holding <c>abc123.jpg</c> and <c>notes.txt</c>, next to a
/// <c>secret.jpg</c> outside the folder. It records every file name the application was asked to approve.
/// </summary>
internal sealed class ImmutableFileTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _root;

    private ImmutableFileTestHost(WebApplication app, string root, HttpClient client, ConcurrentQueue<string> approvals)
    {
        _app = app;
        _root = root;
        Client = client;
        ApprovalQueue = approvals;
    }

    /// <summary>Gets a client for the host.</summary>
    internal HttpClient Client { get; }

    /// <summary>Gets the file names the application was asked to approve, in order.</summary>
    internal IReadOnlyList<string> Approvals => [.. ApprovalQueue];

    private ConcurrentQueue<string> ApprovalQueue { get; }

    /// <summary>Approves every file name.</summary>
    /// <param name="context">The request.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns><see langword="true"/>.</returns>
    internal static ValueTask<bool> ApproveAll(HttpContext context, string fileName) => ValueTask.FromResult(true);

    /// <summary>Starts a host.</summary>
    /// <param name="image">The content of <c>abc123.jpg</c>.</param>
    /// <param name="approve">What the application answers for every name.</param>
    /// <returns>The started host.</returns>
    internal static async Task<ImmutableFileTestHost> Start(byte[] image, bool approve = true)
    {
        var root = Path.Combine(Path.GetTempPath(), $"immutable-files-{Guid.NewGuid():N}");
        var media = Path.Combine(root, "media");
        Directory.CreateDirectory(media);
        await File.WriteAllBytesAsync(Path.Combine(media, "abc123.jpg"), image);
        await File.WriteAllTextAsync(Path.Combine(media, "notes.txt"), "notes");
        await File.WriteAllBytesAsync(Path.Combine(root, "secret.jpg"), image);

        var approvals = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var types = new Dictionary<string, string>(StringComparer.Ordinal) { [".jpg"] = "image/jpeg", [".png"] = "image/png" };
        app.MapImmutableFiles("/media", media, types, (_, fileName) =>
        {
            approvals.Enqueue(fileName);
            return ValueTask.FromResult(approve);
        });
        await app.StartAsync();
        var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        return new ImmutableFileTestHost(app, root, client, approvals);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_root, recursive: true);
    }
}
