using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Inputs, decisions, and output of one pipeline run.</summary>
/// <param name="Inputs">Vault export files, in order.</param>
internal sealed record PipelineRequest(IReadOnlyList<string> Inputs)
{
    /// <summary>Conflict policy; newest wins by default.</summary>
    public MergePolicy Policy { get; init; } = new();

    /// <summary>Finding, field, and match decisions, usually read with <see cref="DecisionsFile"/>.</summary>
    public MergeDecisions Decisions { get; init; } = MergeDecisions.None;

    /// <summary>Where to write the merged vault, or <see langword="null"/> to write nothing.</summary>
    public PipelineOutput? Output { get; init; }
}
