using System.Net;
using System.Net.Http.Headers;

namespace SharedKernel.CI.Forgejo.Tests;

/// <summary>Guards how release attachments are located through the Forgejo API.</summary>
public sealed class ForgejoReleaseAssetsTests
{
    private const string Release = """
        {"id":3,"tag_name":"v1.0.0-alpha.1","assets":[
          {"id":9,"name":"candidate.json","size":10,"browser_download_url":"https://forge.example/owner/name/releases/download/v1.0.0-alpha.1/candidate.json"},
          {"id":10,"name":"A.1.0.0-alpha.1.nupkg","size":10,"browser_download_url":"https://forge.example/owner/name/releases/download/v1.0.0-alpha.1/A.1.0.0-alpha.1.nupkg"}]}
        """;

    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");

    [Fact]
    public async Task Attachments_are_located_by_name_from_one_anonymous_lookup_of_the_tag()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Release);
        using var assets = new ForgejoReleaseAssets(handler, new Uri("https://forge.example"), "owner/name", UserAgent);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var manifest = await assets.Locate("v1.0.0-alpha.1", "candidate.json", ct);
        var package = await assets.Locate("v1.0.0-alpha.1", "A.1.0.0-alpha.1.nupkg", ct);

        // Assert
        manifest.ToString().ShouldBe("https://forge.example/owner/name/releases/download/v1.0.0-alpha.1/candidate.json");
        package.ToString().ShouldBe("https://forge.example/owner/name/releases/download/v1.0.0-alpha.1/A.1.0.0-alpha.1.nupkg");
        handler.Requests.ShouldHaveSingleItem().Message.RequestUri!.ToString().ShouldBe("https://forge.example/api/v1/repos/owner/name/releases/tags/v1.0.0-alpha.1");
        handler.Requests[0].Message.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_attachment_or_release_is_refused()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Release);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        using var assets = new ForgejoReleaseAssets(handler, new Uri("https://forge.example"), "owner/name", UserAgent);
        Func<Task> attachment = () => assets.Locate("v1.0.0-alpha.1", "B.nupkg", TestContext.Current.CancellationToken);
        Func<Task> release = () => assets.Locate("v2.0.0", "candidate.json", TestContext.Current.CancellationToken);

        // Act
        var missingAttachment = await attachment.ShouldThrow<HttpRequestException>();
        var missingRelease = await release.ShouldThrow<HttpRequestException>();

        // Assert
        missingAttachment.Message.ShouldBe("The release https://forge.example/api/v1/repos/owner/name/releases/tags/v1.0.0-alpha.1 has no attachment named B.nupkg.");
        missingRelease.Message.ShouldBe("GET https://forge.example/api/v1/repos/owner/name/releases/tags/v2.0.0 returned 404 NotFound.");
    }

    [Fact]
    public void A_repository_that_is_not_owner_slash_name_is_rejected()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();

        // Act
        Action create = () => new ForgejoReleaseAssets(handler, new Uri("https://forge.example"), "owner/name/extra", UserAgent).Dispose();

        // Assert
        var exception = create.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("repository");
        exception.Message.ShouldBe("The repository must be owner/name, each part of ASCII letters, digits, '-', '.', and '_', and neither part '.' nor '..'. (Parameter 'repository')");
    }
}
