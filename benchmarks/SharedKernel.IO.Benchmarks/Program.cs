using PandaBear.IO;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

const int maximum = 32 * 1024 * 1024;
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
var app = builder.Build();
await using var appLifetime = app.ConfigureAwait(false);
app.MapPost("/{strategy}", async (HttpContext context, string strategy) =>
{
    using var lease = strategy == "lease"
        ? await SeekableContent.Create(context.Request.Body, maximum, 1024 * 1024, context.RequestAborted).ConfigureAwait(false)
        : null;
    using Stream content = lease?.Content ?? (strategy == "memory"
        ? new MemoryStream()
        : new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose));
    var buffer = lease is null ? new byte[81920] : [];
    while (lease is null)
    {
        var read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false);
        if (read == 0)
        {
            break;
        }

        if (content.Length + read > maximum)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        await content.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted).ConfigureAwait(false);
    }

    for (var pass = 0; pass < 3; pass++)
    {
        content.Position = 0;
        _ = await SHA256.HashDataAsync(content, context.RequestAborted).ConfigureAwait(false);
    }

    context.Response.StatusCode = StatusCodes.Status204NoContent;
});
await app.StartAsync().ConfigureAwait(false);
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(address) };
var header = "strategy,bytes,requests,mean_ms,requests_per_second,server_and_client_allocated_bytes_per_request";
await Console.Out.WriteLineAsync(header).ConfigureAwait(false);
foreach (var size in new[] { 64 * 1024, 8 * 1024 * 1024 })
{
    var payload = new byte[size];
    foreach (var strategy in new[] { "memory", "file", "lease" })
    {
        for (var warmup = 0; warmup < 3; warmup++)
        {
            using var body = new ByteArrayContent(payload);
            using var response = await client.PostAsync(new Uri(strategy, UriKind.Relative), body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        var allocated = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        const int count = 20;
        for (var iteration = 0; iteration < count; iteration++)
        {
            using var body = new ByteArrayContent(payload);
            using var response = await client.PostAsync(new Uri(strategy, UriKind.Relative), body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        watch.Stop();
        allocated = GC.GetTotalAllocatedBytes(true) - allocated;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{strategy},{size},{count},{watch.Elapsed.TotalMilliseconds / count:F3},{count / watch.Elapsed.TotalSeconds:F3},{allocated / count}"));
    }
}

await app.StopAsync().ConfigureAwait(false);
