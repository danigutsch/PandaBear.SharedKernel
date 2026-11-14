using System.Globalization;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>A detected secret, described only by its location. It never holds the secret value.</summary>
internal sealed record SecretFinding
{
    /// <summary>Stable identifier: <c>&lt;ruleId&gt;@&lt;fieldPath&gt;:&lt;start&gt;</c>.</summary>
    public string Id => string.Create(CultureInfo.InvariantCulture, $"{RuleId}@{Location.FieldPath}:{Location.Start}");

    /// <summary>Identifier of the rule or detector that produced the finding.</summary>
    public required string RuleId { get; init; }

    /// <summary>Kind of secret.</summary>
    public required SecretKind Kind { get; init; }

    /// <summary>How certain the finding is.</summary>
    public required Confidence Confidence { get; init; }

    /// <summary>Where the value sits.</summary>
    public required SecretLocation Location { get; init; }

    /// <summary>Where a label belonging to the value sits, such as a security question. Never part of the value.</summary>
    public SecretLocation? LabelLocation { get; init; }

    /// <summary>Length of the value in characters.</summary>
    public int ValueLength => Location.Length;

    /// <summary>Proposed destination.</summary>
    public required PlacementTarget ProposedTarget { get; init; }

    /// <summary>Whether the finding is accepted unless the user rejects it. True for high confidence only.</summary>
    public bool IsPreAccepted => Confidence == Confidence.High && !NeedsReview;

    /// <summary>Whether a person must look at the finding before anything happens.</summary>
    public bool NeedsReview { get; init; }

    /// <summary>Human-readable description. It never contains the value.</summary>
    public required string Message { get; init; }

    /// <summary>Describes the finding without its value.</summary>
    /// <returns>The description.</returns>
    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Id} {Kind} {Confidence} length {ValueLength} -> {ProposedTarget}{(NeedsReview ? " (needs review)" : string.Empty)}: {Message}");
    }
}
