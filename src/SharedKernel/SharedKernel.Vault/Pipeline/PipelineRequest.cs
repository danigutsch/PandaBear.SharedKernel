namespace PandaBear.Vault.Pipeline;

/// <summary>Inputs, decisions, and output of one pipeline run.</summary>
/// <param name="Inputs">Vault export files, in order.</param>
public sealed record PipelineRequest(IReadOnlyList<string> Inputs) : MergeRequest;
