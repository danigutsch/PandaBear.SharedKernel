using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PandaBear.AI;

/// <summary>
/// Generates image accessibility text through a LiteLLM OpenAI-compatible proxy.
/// </summary>
public sealed class LiteLlmImageTextGenerator(HttpClient httpClient, LiteLlmImageTextGeneratorOptions options) : IImageTextGenerator
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="ArgumentException">The image, image content type, or language is missing or invalid.</exception>
    /// <exception cref="InvalidOperationException">Required LiteLLM configuration is missing or the image byte limit is not positive.</exception>
    /// <exception cref="ImageTextGenerationException">The image exceeds the configured byte budget, the HTTP request fails, or generated content is unusable.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public async ValueTask<ImageTextGenerationResult> GenerateImageText(ImageTextGenerationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Image is null)
        {
            throw new ArgumentException("The image stream is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ContentType) || !request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The content type must be an image media type.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Language))
        {
            throw new ArgumentException("The language is required.", nameof(request));
        }

        var optionsSnapshot = ValidateOptions();
        var model = optionsSnapshot.Model ?? throw new InvalidOperationException("LiteLLM model is not configured.");
        using var payload = await CreatePayload(request, model, optionsSnapshot.MaxImageBytes, ct).ConfigureAwait(false);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, optionsSnapshot.ChatCompletionsPath)
        {
            Content = payload
        };

        if (!string.IsNullOrWhiteSpace(optionsSnapshot.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", optionsSnapshot.ApiKey);
        }

        using var response = await Send(httpRequest, ct).ConfigureAwait(false);

        LiteLlmChatCompletionResponse completion;
        try
        {
            completion = await response.Content.ReadFromJsonAsync(LiteLlmJsonContext.Default.LiteLlmChatCompletionResponse, ct).ConfigureAwait(false)
                ?? throw new ImageTextGenerationException("LiteLLM response body was empty.");
        }
        catch (JsonException exception)
        {
            throw new ImageTextGenerationException("LiteLLM response body was not valid JSON.", exception);
        }

        var content = completion.Choices.Count == 0 ? null : completion.Choices[0].Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ImageTextGenerationException("LiteLLM response did not contain generated content.");
        }

        try
        {
            var generated = JsonSerializer.Deserialize(content, LiteLlmJsonContext.Default.GeneratedImageText)
                ?? throw new ImageTextGenerationException("LiteLLM generated content was empty.");

            if (string.IsNullOrWhiteSpace(generated.AltText))
            {
                throw new ImageTextGenerationException("LiteLLM generated content did not include alt text.");
            }

            return new ImageTextGenerationResult(generated.AltText.Trim(), string.IsNullOrWhiteSpace(generated.Caption) ? null : generated.Caption.Trim());
        }
        catch (JsonException exception)
        {
            throw new ImageTextGenerationException("LiteLLM generated content was not valid JSON.", exception);
        }
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (HttpRequestException exception)
        {
            throw new ImageTextGenerationException("LiteLLM request failed.", exception);
        }
    }

    private LiteLlmImageTextGeneratorOptions ValidateOptions()
    {
        if (options.Endpoint is null)
        {
            throw new InvalidOperationException("LiteLLM endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("LiteLLM model is not configured.");
        }

        var path = string.IsNullOrWhiteSpace(options.ChatCompletionsPath) ? "/v1/chat/completions" : options.ChatCompletionsPath;
        if (!path.StartsWith('/'))
        {
            throw new InvalidOperationException("LiteLLM chat-completions path must be relative to the endpoint root.");
        }

        if (options.MaxImageBytes <= 0)
        {
            throw new InvalidOperationException("LiteLLM maximum image bytes must be positive.");
        }

        httpClient.BaseAddress ??= options.Endpoint;

        return options with
        {
            Model = options.Model.Trim(),
            ChatCompletionsPath = path
        };
    }

    private static async Task<HttpContent> CreatePayload(ImageTextGenerationRequest request, string model, int maxImageBytes, CancellationToken ct)
    {
        using var imageBuffer = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var remaining = maxImageBytes - imageBuffer.Length;
            var readLength = (int)Math.Min(buffer.Length, remaining + 1);
            var bytesRead = await request.Image.ReadAsync(buffer.AsMemory(0, readLength), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (bytesRead == 0)
            {
                break;
            }

            if (bytesRead > remaining)
            {
                throw new ImageTextGenerationException("Image exceeds the configured maximum image bytes.");
            }

            await imageBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
        }

        var dataUrl = $"data:{request.ContentType};base64,{Convert.ToBase64String(imageBuffer.ToArray())}";

        var context = string.IsNullOrWhiteSpace(request.Context) ? "None supplied." : request.Context.Trim();
        var location = request.Latitude is null || request.Longitude is null
            ? "None supplied."
            : string.Create(CultureInfo.InvariantCulture, $"Latitude {request.Latitude.Value}; longitude {request.Longitude.Value}.");
        var prompt = $"""
            Generate draft accessibility text for this travel catalog image in {request.Language}.

            Return JSON with altText and caption fields only.
            Alt text must describe visible, relevant content for screen-reader users.
            Caption may be null when no useful public caption is warranted.
            Treat all output as draft text requiring human review.
            Do not mark the image decorative.
            Do not guess identity, protected traits, emotions, intent, location, or facts not visible in the image or supplied below.

            Supplied editorial context: {context}
            Supplied location metadata: {location}
            """;

        var payload = new LiteLlmChatCompletionRequest(
            model,
            [new LiteLlmMessage("user", [new LiteLlmContentPart("text", prompt, null), new LiteLlmContentPart("image_url", null, new LiteLlmImageUrl(dataUrl))])],
            new LiteLlmResponseFormat(
                "json_schema",
                new LiteLlmJsonSchema(
                    "image_accessibility_text",
                    new LiteLlmSchema(
                        "object",
                        new Dictionary<string, LiteLlmSchemaProperty>(StringComparer.Ordinal)
                        {
                            ["altText"] = new("string"),
                            ["caption"] = new(["string", "null"])
                        },
                        ["altText", "caption"],
                        false),
                    true)));

        return new StringContent(
            JsonSerializer.Serialize(payload, LiteLlmJsonContext.Default.LiteLlmChatCompletionRequest),
            Encoding.UTF8,
            "application/json");
    }
}
