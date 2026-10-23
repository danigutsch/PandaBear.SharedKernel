using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Tests;

[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class ForwardedClientIpTests
{
    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10")]
    [InlineData("::ffff:203.0.113.10", "203.0.113.10")]
    [InlineData("2001:db8::10", "2001:db8::10")]
    public async Task Forwards_only_the_resolved_address_and_overwrites_supplied_forwarding(string address, string expected)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("forwarded")
            .AddForwardedClientIp()
            .ConfigurePrimaryHttpMessageHandler(() => new ForwardedClientIpTestHandler((request, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Join(',', request.Headers.GetValues(ForwardedHeadersDefaults.XForwardedForHeaderName)))
                })));
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        context.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName] = "198.51.100.99, malformed";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("forwarded");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://catalog.example.test/media");
        request.Headers.Add(ForwardedHeadersDefaults.XForwardedForHeaderName, "198.51.100.20");
        request.Headers.Add(ForwardedHeadersDefaults.XForwardedForHeaderName, "198.51.100.21");

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var forwarded = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        forwarded.ShouldBe(expected);
    }

    [Fact]
    public async Task Pooled_client_uses_the_current_context_without_carrying_identity_between_requests()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("forwarded")
            .AddForwardedClientIp()
            .ConfigurePrimaryHttpMessageHandler(() => new ForwardedClientIpTestHandler((request, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Join(',', request.Headers.GetValues(ForwardedHeadersDefaults.XForwardedForHeaderName)))
                })));
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("forwarded");
        var addresses = new[] { "203.0.113.10", "203.0.113.20" };
        var observed = new List<string>();

        // Act
        foreach (var address in addresses)
        {
            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);
            using var response = await client.GetAsync(new Uri("https://catalog.example.test/media"), TestContext.Current.CancellationToken);
            observed.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        // Assert
        observed.ToArray().ShouldBe(addresses);
    }

    [Fact]
    public async Task Concurrent_requests_through_one_pooled_client_keep_their_own_resolved_addresses()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("forwarded")
            .AddForwardedClientIp()
            .ConfigurePrimaryHttpMessageHandler(() => new ForwardedClientIpTestHandler(async (request, _) =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Join(',', request.Headers.GetValues(ForwardedHeadersDefaults.XForwardedForHeaderName)))
                };
            }));
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("forwarded");
        var addresses = Enumerable.Range(1, 16).Select(index => $"203.0.113.{index}").ToArray();

        // Act
        var observed = await Task.WhenAll(addresses.Select(address => Task.Run(async () =>
        {
            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);
            using var response = await client.GetAsync(new Uri("https://catalog.example.test/media"), TestContext.Current.CancellationToken);
            return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken)));

        // Assert
        observed.ShouldBe(addresses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_request_or_address_removes_stale_forwarding_and_preserves_other_headers(bool hasContext)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("forwarded")
            .AddForwardedClientIp()
            .ConfigurePrimaryHttpMessageHandler(() => new ForwardedClientIpTestHandler((request, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"{request.Headers.Contains(ForwardedHeadersDefaults.XForwardedForHeaderName)}:{request.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem()}")
                })));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = hasContext ? new DefaultHttpContext() : null;
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("forwarded");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://catalog.example.test/media");
        request.Headers.Add(ForwardedHeadersDefaults.XForwardedForHeaderName, "198.51.100.99");
        request.Headers.Add("X-Correlation-Id", "existing-request");

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var observed = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        observed.ShouldBe("False:existing-request");
    }

    [Fact]
    public async Task Registration_does_not_change_clients_that_have_not_opted_in()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient("forwarded").AddForwardedClientIp();
        services.AddHttpClient("ordinary")
            .ConfigurePrimaryHttpMessageHandler(() => new ForwardedClientIpTestHandler((request, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Join(',', request.Headers.GetValues(ForwardedHeadersDefaults.XForwardedForHeaderName)))
                })));
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ordinary");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://catalog.example.test/media");
        request.Headers.Add(ForwardedHeadersDefaults.XForwardedForHeaderName, "198.51.100.99");

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var observed = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        observed.ShouldBe("198.51.100.99");
    }

    [Fact]
    public async Task Delegates_the_same_response_and_preserves_request_and_response_ownership()
    {
        // Arrange
        HttpResponseMessage? expected = null;
        using var handler = new ResolvedClientIpForwardingHandler(new HttpContextAccessor())
        {
            InnerHandler = new ForwardedClientIpTestHandler(async (_, _) =>
            {
                await Task.Yield();
                expected = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("response-body") };
                return expected;
            })
        };
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://catalog.example.test/media")
        {
            Content = new StringContent("request-body")
        };

        // Act
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        var requestBody = await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.ShouldBeSameAs(expected);
        requestBody.ShouldBe("request-body");
        responseBody.ShouldBe("response-body");
    }

    [Fact]
    public async Task Delegates_cancellation_without_replacing_the_callers_token()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        CancellationToken observed = default;
        using var handler = new ResolvedClientIpForwardingHandler(new HttpContextAccessor())
        {
            InnerHandler = new ForwardedClientIpTestHandler((_, token) =>
            {
                observed = token;
                return Task.FromCanceled<HttpResponseMessage>(token);
            })
        };
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://catalog.example.test/media");
        Func<Task> send = async () => await invoker.SendAsync(request, cancellation.Token);

        // Act
        await send.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        observed.ShouldBe(cancellation.Token);
    }

    [Fact]
    public void Rejects_a_null_client_builder()
    {
        // Arrange
        Action register = () => AspNetCoreForwardedHeadersExtensions.AddForwardedClientIp(null!);

        // Act
        var exception = register.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("builder");
    }
}
