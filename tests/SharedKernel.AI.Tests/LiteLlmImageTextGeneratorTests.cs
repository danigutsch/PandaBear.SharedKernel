using System.Net;
using System.Net.Http.Headers;

namespace SharedKernel.AI.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ImageTextGenerationCapability)]
public sealed class LiteLlmImageTextGeneratorTests
{
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
        using var handler = new CapturingHttpMessageHandler(response);
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

        handler.Request.ShouldNotBeNull();
        handler.Request.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri.ShouldBe(new Uri("https://litellm.example/v1/chat/completions"));
        handler.Request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", "local-key"));

        handler.RequestBody.ShouldNotBeNull();
        handler.RequestBody.ShouldContain("\"model\":\"local/vision\"", StringComparison.Ordinal);
        handler.RequestBody.ShouldContain("\"response_format\"", StringComparison.Ordinal);
        handler.RequestBody.ShouldContain("\"json_schema\"", StringComparison.Ordinal);
        handler.RequestBody.ShouldContain("\"image_url\"", StringComparison.Ordinal);
        handler.RequestBody.ShouldContain("data:image/png;base64,AQID", StringComparison.Ordinal);
        handler.RequestBody.ShouldContain("Tour hero image", StringComparison.Ordinal);
        handler.RequestBody.ShouldContain("Latitude 12.34; longitude -56.78.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_image_text_rejects_malformed_generated_json()
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
        using var handler = new CapturingHttpMessageHandler(response);
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
    public async Task Generate_image_text_wraps_litellm_http_failures()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
        using var handler = new CapturingHttpMessageHandler(response);
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
