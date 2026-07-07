namespace SharedKernel.AI;

/// <summary>
/// Configures the LiteLLM OpenAI-compatible image text generation client.
/// </summary>
public sealed record LiteLlmImageTextGeneratorOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "AI:ImageText:LiteLlm";

    /// <summary>
    /// Gets the LiteLLM proxy base endpoint.
    /// </summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// Gets the optional bearer key for the LiteLLM proxy.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the vision-capable model name configured in LiteLLM.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the chat-completions path exposed by the LiteLLM proxy.
    /// </summary>
    public string ChatCompletionsPath { get; init; } = "/v1/chat/completions";
}
