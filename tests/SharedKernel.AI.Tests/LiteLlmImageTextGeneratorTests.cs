using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.AI.Tests;

/// <summary>Tests the request shape and error handling of the LiteLLM image text generator against a stubbed HTTP handler.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ImageTextGenerationCapability)]
public sealed class LiteLlmImageTextGeneratorTests
{
    [Theory]
    [InlineData("""{"prompt_tokens":12,"completion_tokens":7}""", "4.5e-3", 12L, 7L, "0.0045")]
    [InlineData("""{"prompt_tokens":0,"completion_tokens":"unknown"}""", "invalid", 0L, null, null)]
    [InlineData("""{"completion_tokens":9}""", null, null, 9L, null)]
    [InlineData("""{"prompt_tokens":-1,"completion_tokens":2.5}""", "-1.5", null, null, null)]
    [InlineData("[]", "0.003", null, null, "0.003")]
    [InlineData(null, null, null, null, null)]
    [InlineData("""{"completion_tokens":9223372036854775808}""", "0.004|0.005", null, null, null)]
    public async Task Generate_image_text_exposes_only_reported_usage(
        string? bodyUsage, string? costHeader, long? inputTokens, long? outputTokens, string? costUsd)
    {
        // Arrange
        var usageJson = bodyUsage is null ? string.Empty : ",\"usage\":" + bodyUsage;
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$$"""
                {"choices":[{"message":{"content":"{\"altText\":\"Draft text\"}"}}]{{{usageJson}}}
                }
                """)
        };
        if (costHeader is not null)
        {
            response.Headers.Add("x-litellm-response-cost", costHeader.Split('|'));
        }

        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(
            httpClient,
            new LiteLlmImageTextGeneratorOptions
            {
                Endpoint = new Uri("https://litellm.example"),
                Model = "local/vision"
            });

        // Act
        var result = await generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = Stream.Null, ContentType = "image/jpeg", Language = "en-US" },
            TestContext.Current.CancellationToken);

        // Assert
        result.AltText.ShouldBe("Draft text");
        var expectedCost = costUsd is not null ? decimal.Parse(costUsd, CultureInfo.InvariantCulture) : (decimal?)null;
        if (inputTokens is null && outputTokens is null && expectedCost is null)
        {
            result.Usage.ShouldBeNull();
            return;
        }

        result.Usage.ShouldNotBeNull().ShouldBe(new ImageTextGenerationUsage(inputTokens, outputTokens, expectedCost));
    }

    [Fact]
    public async Task Generate_image_text_sends_openai_compatible_image_request()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"altText\":\"Sunny beach\",\"caption\":\"Beach view\"}"
                  }
                }
              ]
            }
            """)
        };
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(
            httpClient,
            new LiteLlmImageTextGeneratorOptions
            {
                Endpoint = new Uri("https://litellm.example"),
                ApiKey = "local-key",
                Model = "local/vision"
            });

        // Act
        var result = await generator.GenerateImageText(
            new ImageTextGenerationRequest
            {
                Image = new MemoryStream([1, 2, 3]),
                ContentType = "image/png",
                Language = "en-US",
                Context = "Tour hero image",
                Latitude = 12.34m,
                Longitude = -56.78m
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.AltText.ShouldBe("Sunny beach");
        result.Caption.ShouldBe("Beach view");

        var request = handler.LastRequest.ShouldNotBeNull();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Message.RequestUri.ShouldBe(new Uri("https://litellm.example/v1/chat/completions"));
        request.Message.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", "local-key"));

        request.Body.ShouldNotBeNull();
        request.Body.ShouldContain("\"model\":\"local/vision\"", StringComparison.Ordinal);
        request.Body.ShouldContain("\"response_format\"", StringComparison.Ordinal);
        request.Body.ShouldContain("\"json_schema\"", StringComparison.Ordinal);
        request.Body.ShouldContain("\"image_url\"", StringComparison.Ordinal);
        request.Body.ShouldContain("data:image/png;base64,AQID", StringComparison.Ordinal);
        request.Body.ShouldContain("Tour hero image", StringComparison.Ordinal);
        request.Body.ShouldContain("Latitude 12.34; longitude -56.78.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_image_text_rejects_malformed_generated_JSON()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "not-json"
                  }
                }
              ]
            }
            """)
        };
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(
            httpClient,
            new LiteLlmImageTextGeneratorOptions
            {
                Endpoint = new Uri("https://litellm.example"),
                Model = "local/vision"
            });

        // Act
        var action = () => generator.GenerateImageText(
            new ImageTextGenerationRequest
            {
                Image = new MemoryStream([1]),
                ContentType = "image/jpeg",
                Language = "en-US"
            },
            TestContext.Current.CancellationToken).AsTask();

        // Assert
        var exception = await action.ShouldThrow<ImageTextGenerationException>();
        exception.Message.ShouldBe("LiteLLM generated content was not valid JSON.");
    }

    [Fact]
    public async Task Generate_image_text_wraps_litellm_HTTP_failures()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(
            httpClient,
            new LiteLlmImageTextGeneratorOptions
            {
                Endpoint = new Uri("https://litellm.example"),
                Model = "local/vision"
            });

        // Act
        var action = () => generator.GenerateImageText(
            new ImageTextGenerationRequest
            {
                Image = new MemoryStream([1]),
                ContentType = "image/jpeg",
                Language = "en-US"
            },
            TestContext.Current.CancellationToken).AsTask();

        // Assert
        var exception = await action.ShouldThrow<ImageTextGenerationException>();
        exception.Message.ShouldBe("LiteLLM request failed.");
    }
}
