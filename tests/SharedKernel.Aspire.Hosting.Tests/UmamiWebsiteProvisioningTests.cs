using System.Net;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>
/// Tests how a website is set up in a running Umami: signing in, creating the website with its id only when it does not exist, and
/// reporting the outcome on the website resource without the password or the token.
/// </summary>
public sealed class UmamiWebsiteProvisioningTests
{
    private const string ChangedPassword = "password-secret";

    private static readonly Uri Endpoint = new("http://localhost:3300");

    private static readonly Guid WebsiteId = Guid.Parse("8f1d8a3e-7c2b-4d5e-9f60-1a2b3c4d5e6f");

    [Fact]
    public async Task A_fresh_umami_is_signed_in_to_as_admin_and_gets_the_website_with_its_ID()
    {
        // Arrange
        using var umami = new FakeUmami();
        using var client = new HttpClient(umami);
        var api = new UmamiApiClient(client, Endpoint);

        // Act
        var outcome = await api.EnsureWebsite("umami", WebsiteId, "site", "example.com", TestContext.Current.CancellationToken);

        // Assert
        outcome.ShouldBe(UmamiWebsiteOutcome.Created);
        umami.Websites.ShouldBe([WebsiteId]);
        var requests = umami.Requests;
        requests.Select(request => $"{request.Method} {request.Path}").ToArray().ShouldBe(
            ["POST /api/auth/login", $"GET /api/websites/{WebsiteId}", "POST /api/websites"]);
        using var login = JsonDocument.Parse(requests[0].Body!);
        login.RootElement.GetProperty("username").GetString().ShouldBe("admin");
        login.RootElement.GetProperty("password").GetString().ShouldBe("umami");
        requests[1].Authorization.ShouldBe($"Bearer {FakeUmami.Token}");
        requests[2].Authorization.ShouldBe($"Bearer {FakeUmami.Token}");
        using var created = JsonDocument.Parse(requests[2].Body!);
        created.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray().ShouldBe(["domain", "id", "name"]);
        created.RootElement.GetProperty("id").GetGuid().ShouldBe(WebsiteId);
        created.RootElement.GetProperty("name").GetString().ShouldBe("site");
        created.RootElement.GetProperty("domain").GetString().ShouldBe("example.com");
    }

    [Fact]
    public async Task A_website_that_exists_is_not_created_again()
    {
        // Arrange
        using var umami = new FakeUmami();
        umami.Websites.Add(WebsiteId);
        using var client = new HttpClient(umami);
        var api = new UmamiApiClient(client, Endpoint);

        // Act
        var outcome = await api.EnsureWebsite("umami", WebsiteId, "site", "example.com", TestContext.Current.CancellationToken);

        // Assert
        outcome.ShouldBe(UmamiWebsiteOutcome.AlreadyExists);
        umami.Requests.ShouldNotContain(request => request.Method == HttpMethod.Post && request.Path == "/api/websites");
    }

    [Fact]
    public async Task A_refused_sign_in_fails_at_sign_in_with_its_status_and_without_the_password()
    {
        // Arrange
        using var umami = new FakeUmami { AdminPassword = "another" };
        using var client = new HttpClient(umami);
        var api = new UmamiApiClient(client, Endpoint);

        // Act
        var ensure = () => api.EnsureWebsite(ChangedPassword, WebsiteId, "site", "example.com", TestContext.Current.CancellationToken);

        // Assert
        var exception = await ensure.ShouldThrow<UmamiProvisioningException>();
        exception.Step.ShouldBe(UmamiProvisioningStep.SignIn);
        exception.StatusCode.ShouldBe(401);
        exception.ToString().ShouldNotContain(ChangedPassword, StringComparison.Ordinal);
        umami.Websites.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_refused_website_fails_at_create_with_its_status_and_without_the_token()
    {
        // Arrange
        using var umami = new FakeUmami { CreateFailure = HttpStatusCode.BadRequest };
        using var client = new HttpClient(umami);
        var api = new UmamiApiClient(client, Endpoint);

        // Act
        var ensure = () => api.EnsureWebsite("umami", WebsiteId, "site", "example.com", TestContext.Current.CancellationToken);

        // Assert
        var exception = await ensure.ShouldThrow<UmamiProvisioningException>();
        exception.Step.ShouldBe(UmamiProvisioningStep.Create);
        exception.StatusCode.ShouldBe(400);
        exception.ToString().ShouldNotContain(FakeUmami.Token, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sign_in_that_asks_for_a_second_factor_fails_at_sign_in()
    {
        // Arrange
        using var umami = new FakeUmami { RequiresTwoFactor = true };
        using var client = new HttpClient(umami);
        var api = new UmamiApiClient(client, Endpoint);

        // Act
        var ensure = () => api.EnsureWebsite("umami", WebsiteId, "site", "example.com", TestContext.Current.CancellationToken);

        // Assert
        var exception = await ensure.ShouldThrow<UmamiProvisioningException>();
        exception.Step.ShouldBe(UmamiProvisioningStep.SignIn);
        exception.StatusCode.ShouldBeNull();
    }

    [Fact]
    public async Task A_website_that_was_set_up_is_running_and_its_log_says_so()
    {
        // Arrange
        using var umami = new FakeUmami();
        var builder = DistributedApplication.CreateBuilder([]);
        var website = builder.AddUmami("umami").AddWebsite("site", "example.com");
        await using var run = UmamiProvisioningRun.Create(builder, website, umami);

        // Act
        await run.Provision();

        // Assert
        run.State().ShouldBe(KnownResourceStates.Running);
        var log = await run.Log();
        log.ShouldContain(line => line.Contains(website.Resource.WebsiteId.ToString(), StringComparison.Ordinal) && line.Contains("Created", StringComparison.Ordinal));
        log.ShouldAllSatisfy(line => line.ShouldNotContain(FakeUmami.Token, StringComparison.Ordinal));
        umami.Websites.ShouldBe([website.Resource.WebsiteId]);
    }

    [Fact]
    public async Task A_website_that_could_not_be_set_up_failed_to_start_and_its_log_names_the_step_and_status_without_the_password()
    {
        // Arrange
        using var umami = new FakeUmami { AdminPassword = "another" };
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration["Parameters:umami-admin-password"] = ChangedPassword;
        var password = builder.AddParameter("umami-admin-password", secret: true);
        var website = builder.AddUmami("umami").WithAdminPassword(password).AddWebsite("site", "example.com");
        await using var run = UmamiProvisioningRun.Create(builder, website, umami);

        // Act
        await run.Provision();

        // Assert
        run.State().ShouldBe(KnownResourceStates.FailedToStart);
        var log = await run.Log();
        log.ShouldContain(line => line.Contains("sign", StringComparison.OrdinalIgnoreCase) && line.Contains("401", StringComparison.Ordinal));
        log.ShouldAllSatisfy(line => line.ShouldNotContain(ChangedPassword, StringComparison.Ordinal));
        umami.Websites.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_given_admin_password_is_the_one_used_to_sign_in()
    {
        // Arrange
        using var umami = new FakeUmami { AdminPassword = ChangedPassword };
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration["Parameters:umami-admin-password"] = ChangedPassword;
        var password = builder.AddParameter("umami-admin-password", secret: true);
        var website = builder.AddUmami("umami").WithAdminPassword(password).AddWebsite("site", "example.com");
        await using var run = UmamiProvisioningRun.Create(builder, website, umami);

        // Act
        await run.Provision();

        // Assert
        run.State().ShouldBe(KnownResourceStates.Running);
        umami.Websites.ShouldBe([website.Resource.WebsiteId]);
    }
}
