using System.Net.Http.Headers;

namespace PandaBear.Forgejo.Tests;

/// <summary>Tests the base address and headers of the Forgejo API client.</summary>
public sealed class ForgejoApiClientTests
{
    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");

    [Fact]
    public async Task Requests_go_to_the_API_endpoint_with_the_headers_and_the_token()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        using var client = ForgejoApiClient.Create(handler, new Uri("https://codeberg.org"), UserAgent, "secret");

        // Act
        using var response = await client.GetAsync(new Uri("repos/o/r/releases", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        var request = handler.LastRequest.ShouldNotBeNull().Message;
        request.RequestUri!.ToString().ShouldBe("https://codeberg.org/api/v1/repos/o/r/releases");
        request.Headers.UserAgent.ToString().ShouldBe("sharedkernel-test/1.0");
        request.Headers.Accept.ToString().ShouldBe("application/json");
        request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("token", "secret"));
    }

    [Fact]
    public async Task Anonymous_requests_carry_no_authorization()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        using var client = ForgejoApiClient.Create(handler, new Uri("https://codeberg.org/"), UserAgent, token: null);

        // Act
        using var response = await client.GetAsync(new Uri("version", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        handler.LastRequest.ShouldNotBeNull().Message.Headers.Authorization.ShouldBeNull();
    }

    [Theory]
    [InlineData("https://forge.example/git", "https://forge.example/git/api/v1/")]
    [InlineData("https://forge.example/git/", "https://forge.example/git/api/v1/")]
    [InlineData("https://forge.example:8443", "https://forge.example:8443/api/v1/")]
    public void The_API_endpoint_keeps_the_path_of_an_instance_behind_a_sub_path(string server, string expected) =>
        ForgejoApiClient.ApiBase(new Uri(server)).ToString().ShouldBe(expected);

    [Theory]
    [InlineData("http://forge.example")]
    [InlineData("https://user:secret@forge.example")]
    [InlineData("https://forge.example/?x=1")]
    [InlineData("https://forge.example/#top")]
    public void A_server_that_is_not_a_plain_secure_address_is_refused(string server)
    {
        // Act
        var create = () => ForgejoApiClient.Create(new RecordingHttpMessageHandler(), new Uri(server), UserAgent, null);

        // Assert
        create.ShouldThrow<ArgumentException>().ParamName.ShouldBe("server");
    }

    [Fact]
    public void A_blank_token_is_refused()
    {
        // Act
        var create = () => ForgejoApiClient.Create(new RecordingHttpMessageHandler(), new Uri("https://forge.example"), UserAgent, " ");

        // Assert
        create.ShouldThrow<ArgumentException>();
    }
}
