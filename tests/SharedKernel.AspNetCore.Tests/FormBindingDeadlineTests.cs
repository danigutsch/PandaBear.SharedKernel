using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Testing;
using PandaBear.Testing.Http;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Exercises timeout behavior during implicit multipart binding over a real socket.</summary>
[TestScope(TestScope.Integration)]
public sealed class FormBindingDeadlineTests
{
    [Fact]
    public async Task A_stalled_multipart_binding_honors_the_endpoint_timeout()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var handlerCalls = 0;
        await using var host = await PipelineTestHost.Start(
            services => services.AddRequestTimeouts(),
            app =>
            {
                app.UseRequestTimeouts();
                app.UseFormBindingDeadline();
                app.MapPost("/upload", (IFormFile file) =>
                {
                    Interlocked.Increment(ref handlerCalls);
                    return TypedResults.NoContent();
                })
                .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 65_536 })
                .WithRequestTimeout(TimeSpan.FromSeconds(1))
                .DisableAntiforgery();
            });

        var address = new Uri(host.Client.BaseAddress.ShouldNotBeNull(), "/upload");
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new ByteArrayContent([]) };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=demo");
        using var upload = new LoopbackHttpBodyRequest();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        await upload.Send(request, Encoding.ASCII.GetBytes("--demo\r\n"), declaredLength: 1024, timeout.Token);
        var statusLine = await upload.ReadStatusLine(timeout.Token);

        // Assert
        statusLine.ShouldBe("HTTP/1.1 504 Gateway Timeout");
        handlerCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_complete_multipart_request_binds_with_or_without_a_declared_length(bool declareLength)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = await PipelineTestHost.Start(
            services => services.AddRequestTimeouts(),
            app =>
            {
                app.UseRequestTimeouts();
                app.UseFormBindingDeadline();
                app.MapPost("/upload", (IFormFile file, HttpRequest request) =>
                    TypedResults.Text($"{file.Length}|{request.ContentLength.HasValue}"))
                    .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 65_536 })
                    .WithRequestTimeout(TimeSpan.FromSeconds(10))
                    .DisableAntiforgery();
            });
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "example.txt");
        var body = await form.ReadAsByteArrayAsync(ct);
        var contentType = form.Headers.ContentType.ShouldNotBeNull().ToString();
        using var content = new GatedChunkContent(body, chunkSize: 64, contentType, declareLength);

        for (var chunk = 0; chunk < content.ChunkCount; chunk++)
        {
            content.ReleaseChunk();
        }

        // Act
        using var response = await host.Client.PostAsync(new Uri("/upload", UriKind.Relative), content, ct);
        var result = await response.Content.ReadAsStringAsync(ct);

        // Assert
        result.ShouldBe($"3|{declareLength}");
        content.ChunksStarted.ShouldBe(content.ChunkCount);
        content.BytesAfter(content.ChunkCount).ShouldBe(body.LongLength);
    }
}
