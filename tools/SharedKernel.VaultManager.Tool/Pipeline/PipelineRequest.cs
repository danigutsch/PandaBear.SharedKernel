namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Inputs, decisions, and output of one pipeline run.</summary>
/// <param name="Inputs">Vault export files, in order.</param>
internal sealed record PipelineRequest(IReadOnlyList<string> Inputs) : MergeRequest;
