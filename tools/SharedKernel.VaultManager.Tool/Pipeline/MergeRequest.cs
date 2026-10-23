using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// Decisions, policy, and output of one merge of a scanned vault through <see cref="VaultPipeline.Merge"/>. The review UI
/// changes the decisions and merges again; <see cref="PipelineRequest"/> adds the inputs for a whole run.
/// </summary>
internal record MergeRequest
{
    /// <summary>Conflict policy; newest wins by default.</summary>
    public MergePolicy Policy { get; init; } = new();

    /// <summary>Finding, field, and match decisions, usually read with <see cref="DecisionsFile"/>.</summary>
    public MergeDecisions Decisions { get; init; } = MergeDecisions.None;

    /// <summary>Where to write the merged vault, or <see langword="null"/> to write nothing.</summary>
    public PipelineOutput? Output
    {
        get; init;
    }
}
