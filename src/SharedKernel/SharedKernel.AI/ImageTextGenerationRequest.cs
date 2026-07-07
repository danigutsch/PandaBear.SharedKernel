namespace SharedKernel.AI;

/// <summary>
/// Contains an image and editorial context for AI-assisted accessibility text generation.
/// </summary>
public sealed record ImageTextGenerationRequest
{
    /// <summary>
    /// Gets the source image stream.
    /// </summary>
    public required Stream Image { get; init; }

    /// <summary>
    /// Gets the image media type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Gets the BCP-47 language tag for the generated text.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Gets optional editorial context supplied by a human editor.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets the optional latitude supplied by trusted metadata.
    /// </summary>
    public decimal? Latitude { get; init; }

    /// <summary>
    /// Gets the optional longitude supplied by trusted metadata.
    /// </summary>
    public decimal? Longitude { get; init; }
}
