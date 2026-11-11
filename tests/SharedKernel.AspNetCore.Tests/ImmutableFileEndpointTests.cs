using System.Net;
using Microsoft.AspNetCore.Builder;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Tests the immutable file endpoint: only plain file names with an allow-listed extension that the application approves are served from the
/// folder, with a year-long immutable cache header and the configured media type; everything else is a 404.
/// </summary>
public sealed class ImmutableFileEndpointTests
{
    private static readonly byte[] Image = [0xFF, 0xD8, 0xFF, 0xD9];

    [Fact]
    public async Task An_approved_file_is_served_with_its_media_type_and_an_immutable_cache_header()
    {
        // Arrange
        await using var host = await ImmutableFileTestHost.Start(Image);

        // Act
        using var response = await host.Client.GetAsync(new Uri("/media/abc123.jpg", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("image/jpeg");
        response.Headers.CacheControl.ShouldNotBeNull().ToString().ShouldBe(ImmutableFileEndpointExtensions.CacheControl);
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        body.ShouldBe(Image);
        host.Approvals.ShouldBe(["abc123.jpg"]);
    }

    [Fact]
    public async Task A_head_request_gets_the_headers_without_the_body()
    {
        // Arrange
        await using var host = await ImmutableFileTestHost.Start(Image);
        using var request = new HttpRequestMessage(HttpMethod.Head, "/media/abc123.jpg");

        // Act
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.ShouldBe(Image.Length);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        body.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_file_the_application_does_not_approve_is_not_found()
    {
        // Arrange
        await using var host = await ImmutableFileTestHost.Start(Image, approve: false);

        // Act
        using var response = await host.Client.GetAsync(new Uri("/media/abc123.jpg", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.CacheControl.ShouldBeNull();
    }

    [Fact]
    public async Task An_approved_name_without_a_file_is_not_found()
    {
        // Arrange
        await using var host = await ImmutableFileTestHost.Start(Image);

        // Act
        using var response = await host.Client.GetAsync(new Uri("/media/missing.jpg", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/media/notes.txt")]
    [InlineData("/media/abc123.JPG")]
    [InlineData("/media/abc123")]
    [InlineData("/media/.jpg")]
    [InlineData("/media/.hidden.jpg")]
    [InlineData("/media/..%2Fsecret.jpg")]
    [InlineData("/media/..%5Csecret.jpg")]
    [InlineData("/media/%2E%2E%2Fsecret.jpg")]
    [InlineData("/media/abc%20def.jpg")]
    [InlineData("/media/abc:def.jpg")]
    public async Task A_name_that_is_not_a_plain_allow_listed_file_name_is_refused_before_the_application_is_asked(string path)
    {
        // Arrange
        await using var host = await ImmutableFileTestHost.Start(Image);

        // Act
        using var response = await host.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        host.Approvals.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_name_with_a_control_character_is_never_served_or_offered_for_approval()
    {
        // Arrange
        await using var host = await ImmutableFileTestHost.Start(Image);

        // Act
        using var response = await host.Client.GetAsync(new Uri("/media/abc%00.jpg", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
        host.Approvals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_options_are_rejected_when_mapping()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();
        var types = new Dictionary<string, string>(StringComparer.Ordinal) { [".jpg"] = "image/jpeg" };

        // Act
        Action noPrefix = () => app.MapImmutableFiles("media", Path.GetTempPath(), types, ImmutableFileTestHost.ApproveAll);
        Action noFolder = () => app.MapImmutableFiles("/media", " ", types, ImmutableFileTestHost.ApproveAll);
        Action noTypes = () => app.MapImmutableFiles("/media", Path.GetTempPath(), new Dictionary<string, string>(), ImmutableFileTestHost.ApproveAll);
        Action badType = () => app.MapImmutableFiles("/media", Path.GetTempPath(), new Dictionary<string, string> { ["jpg"] = "image/jpeg" }, ImmutableFileTestHost.ApproveAll);
        Action noApproval = () => app.MapImmutableFiles("/media", Path.GetTempPath(), types, null!);

        // Assert
        noPrefix.ShouldThrow<ArgumentException>();
        noFolder.ShouldThrow<ArgumentException>();
        noTypes.ShouldThrow<ArgumentException>();
        badType.ShouldThrow<ArgumentException>();
        noApproval.ShouldThrow<ArgumentNullException>();
    }
}
