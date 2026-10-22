using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PandaBear.Branding.Tests;

[Trait(PandaBear.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ApiClientCapability)]
public sealed class BrandingResponseLimitTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveSettings_respects_client_buffer_limit_and_disposes_response(bool knownLength)
    {
        using var body = new BrandingResponseReadStream(Encoding.UTF8.GetBytes("{\"brandName\":\"" + new string('x', 1024) + "\"}"), knownLength);
        using var content = new StreamContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (!knownLength)
        {
            content.Headers.ContentLength = null;
        }
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example"),
            MaxResponseContentBufferSize = 128
        };
        var sut = new ManagementBrandingApiClient(client);

        Func<Task> save = () => sut.SaveSettings(BrandingSettingsTestData.ValidRequest(), TestContext.Current.CancellationToken);
        await save.ShouldThrow<HttpRequestException>();
        body.BytesRead.ShouldBeLessThanOrEqualTo(129L);
        body.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveSettings_accepts_json_at_exact_client_limit(bool knownLength)
    {
        const string json = "{\"brandName\":\"Exact\"}";
        using var body = new BrandingResponseReadStream(Encoding.UTF8.GetBytes(json), knownLength);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example"),
            MaxResponseContentBufferSize = Encoding.UTF8.GetByteCount(json)
        };
        var result = await new ManagementBrandingApiClient(client).SaveSettings(BrandingSettingsTestData.ValidRequest(), TestContext.Current.CancellationToken);
        result.BrandName.ShouldBe("Exact");
        body.BytesRead.ShouldBe((long)Encoding.UTF8.GetByteCount(json));
        body.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveSettings_rejects_body_above_default_ceiling()
    {
        const int limit = 1024 * 1024;
        using var body = new BrandingResponseReadStream(Encoding.UTF8.GetBytes("{\"brandName\":\"" + new string('x', limit) + "\"}"), false);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://branding.example") };
        Func<Task> save = () => new ManagementBrandingApiClient(client).SaveSettings(BrandingSettingsTestData.ValidRequest(), TestContext.Current.CancellationToken);
        await save.ShouldThrow<HttpRequestException>();
        body.BytesRead.ShouldBe(limit + 1L);
        body.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveSettings_preserves_declared_charset()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"brandName\":\"Café\"}", Encoding.Unicode, "application/json")
        };
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://branding.example") };
        var settings = await new ManagementBrandingApiClient(client).SaveSettings(BrandingSettingsTestData.ValidRequest(), TestContext.Current.CancellationToken);
        settings.BrandName.ShouldBe("Café");
    }

    [Fact]
    public async Task SaveSettings_counts_actual_bytes_despite_small_declared_length()
    {
        using var body = new BrandingResponseReadStream(Encoding.UTF8.GetBytes("{\"brandName\":\"" + new string('x', 1024) + "\"}"), false);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        response.Content.Headers.ContentLength = 1;
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example"),
            MaxResponseContentBufferSize = 128
        };
        Func<Task> save = () => new ManagementBrandingApiClient(client).SaveSettings(BrandingSettingsTestData.ValidRequest(), TestContext.Current.CancellationToken);
        await save.ShouldThrow<HttpRequestException>();
        body.BytesRead.ShouldBe(129L);
        body.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("null", true)]
    [InlineData("invalid-json", false)]
    public async Task SaveSettings_preserves_unusable_json_exception_contract(string json, bool nullBody)
    {
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://branding.example") };
        Func<Task> save = () => new ManagementBrandingApiClient(client).SaveSettings(BrandingSettingsTestData.ValidRequest(), TestContext.Current.CancellationToken);
        if (nullBody)
        {
            await save.ShouldThrow<InvalidOperationException>();
        }
        else
        {
            await save.ShouldThrow<System.Text.Json.JsonException>();
        }
    }
}
