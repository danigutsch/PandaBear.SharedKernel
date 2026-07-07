namespace SharedKernel.AI;

/// <summary>
/// Contains AI-generated draft image accessibility text.
/// </summary>
/// <param name="AltText">The drafted accessible image description.</param>
/// <param name="Caption">The optional drafted caption.</param>
public sealed record ImageTextGenerationResult(string AltText, string? Caption);
