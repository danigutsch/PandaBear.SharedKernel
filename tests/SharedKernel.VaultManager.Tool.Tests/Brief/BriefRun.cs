using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Brief;

/// <summary>Reads, scans, and merges inputs as the merge command does, and builds the brief of the result.</summary>
internal static class BriefRun
{
    public static (BriefDocument Brief, PipelineResult Result) Build(IReadOnlyList<string> inputs, MergeDecisions? decisions = null, MergePolicy? policy = null)
    {
        decisions ??= MergeDecisions.None;
        var pipeline = new VaultPipeline(new TestPrompts("fixture export password").Environment());
        ScannedVault scanned = VaultPipeline.Scan(pipeline.Load(inputs));
        PipelineResult result = pipeline.Merge(scanned, new MergeRequest { Policy = policy ?? new MergePolicy(), Decisions = decisions });
        BriefInput[] files = [.. inputs.Select(input => new BriefInput(Path.GetFileName(input), CatalogFile.Hash(input)))];
        return (BriefBuilder.Build(files, scanned, result, decisions), result);
    }

    /// <summary>The field of a brief group with the given path; it must be listed exactly once.</summary>
    public static BriefField Field(BriefGroup group, string fieldPath) =>
        group.Fields.ShouldHaveSingleItem(field => string.Equals(field.FieldPath, fieldPath, StringComparison.Ordinal));

    /// <summary>Decisions that link two items, as a user links items with different user names, which do not match on their own.</summary>
    public static MergeDecisions Linked(string first, string second) => new([], new MatchOverrides([new OriginKeyPair(first, second)], []));
}
