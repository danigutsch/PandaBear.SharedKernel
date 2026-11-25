namespace PandaBear.AI;

/// <summary>Contains optional provider-reported measurements for a completed generation.</summary>
/// <param name="InputTokens">Input token count, when reported.</param>
/// <param name="OutputTokens">Output token count, when reported.</param>
/// <param name="CostUsd">Reported cost in US dollars, when available.</param>
public sealed record ImageTextGenerationUsage(long? InputTokens, long? OutputTokens, decimal? CostUsd);
