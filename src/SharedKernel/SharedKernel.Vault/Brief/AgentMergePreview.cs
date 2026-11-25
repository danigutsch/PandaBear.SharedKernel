using SharedKernel.Vault.Merge;
using SharedKernel.Vault.Pipeline;

namespace SharedKernel.Vault.Brief;

/// <summary>Evaluates saved agent decisions on a scanned vault and returns a preview without writing an export.</summary>
public sealed class AgentMergePreview
{
    private AgentMergePreview(PipelineResult result, MergeDecisions decisions)
    {
        Result = result;
        Decisions = decisions;
    }

    private AgentMergePreview(AgentMergeFailure failure, IReadOnlyList<string> problems)
    {
        Failure = failure;
        Problems = problems;
    }

    /// <summary>The merged preview when every validation stage succeeds.</summary>
    public PipelineResult? Result { get; }

    /// <summary>The combined decisions with the user's decisions first, when validation succeeds.</summary>
    public MergeDecisions? Decisions { get; }

    /// <summary>The first validation stage that rejected the decisions, if any.</summary>
    public AgentMergeFailure? Failure { get; }

    /// <summary>Validation messages containing identifiers and field paths without vault values.</summary>
    public IReadOnlyList<string> Problems { get; } = [];

    /// <summary>Checks the saved scope, revalidates it against the scanned vault, and previews the combined decisions.</summary>
    /// <param name="pipeline">The pipeline for the user-only and combined previews.</param>
    /// <param name="scanned">The already scanned vault in input order.</param>
    /// <param name="request">The user's merge policy and decisions. The preview ignores its requested output.</param>
    /// <param name="brief">The saved brief the agent read.</param>
    /// <param name="agent">The decisions the agent proposed.</param>
    /// <param name="ct">Cancellation token for validation and merging.</param>
    /// <returns>The accepted preview and combined decisions, or the failure stage and validation messages.</returns>
    /// <exception cref="ArgumentNullException">One of the required arguments is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    public static AgentMergePreview Evaluate(
        VaultPipeline pipeline,
        ScannedVault scanned,
        MergeRequest request,
        BriefDocument brief,
        MergeDecisions agent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(scanned);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(agent);
        ct.ThrowIfCancellationRequested();

        if (!scanned.InputPaths.Select(Path.GetFileName).OfType<string>().SequenceEqual(brief.Inputs, StringComparer.Ordinal))
        {
            return new(AgentMergeFailure.InputFiles, ["the scanned input file names or their order differ from the saved brief."]);
        }

        var problems = AgentDecisions.Check(agent, brief);
        if (problems.Count > 0)
        {
            return new(AgentMergeFailure.SavedBrief, problems);
        }

        MergeRequest preview = request with { Output = null };
        PipelineResult userOnly = pipeline.Merge(scanned, preview, ct);

        // Include every group, since a decision the user made since the brief may have resolved one.
        BriefDocument current = BriefBuilder.Build(brief.Inputs, scanned, userOnly, request.Decisions, everyGroup: true);
        problems = AgentDecisions.Check(agent, current);
        if (problems.Count > 0)
        {
            return new(AgentMergeFailure.CurrentInputs, problems);
        }

        MergeDecisions decisions = AgentDecisions.Combine(request.Decisions, agent);
        PipelineResult applied = pipeline.Merge(scanned, preview with { Decisions = decisions }, ct);
        problems = AgentDecisions.Conflicts(userOnly, applied, decisions);
        return problems.Count == 0 ? new(applied, decisions) : new(AgentMergeFailure.ChangedMerge, problems);
    }
}
