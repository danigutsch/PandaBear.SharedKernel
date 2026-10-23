using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Files a review writes.</summary>
/// <param name="DecisionsPath">Decisions file that saving writes, or <see langword="null"/> when the review cannot save decisions.</param>
/// <param name="Output">Merged vault that writing creates, or <see langword="null"/> when the review cannot write it.</param>
internal sealed record ReviewFiles(string? DecisionsPath, PipelineOutput? Output)
{
    /// <summary>A review that neither saves decisions nor writes a merged vault.</summary>
    public static ReviewFiles None { get; } = new(null, null);
}
