using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>One catalog detection rule.</summary>
internal sealed record SecretRule
{
    /// <summary>Stable rule identifier, used in finding identifiers.</summary>
    public required string Id
    {
        get; init;
    }

    /// <summary>Name shown in reports and used for hidden field names.</summary>
    public required string DisplayName
    {
        get; init;
    }

    /// <summary>Kind of secret the rule finds.</summary>
    public required SecretKind Kind
    {
        get; init;
    }

    /// <summary>Case-insensitive keywords; the pattern only runs on windows around keyword hits.</summary>
    public required IReadOnlyList<string> Keywords
    {
        get; init;
    }

    /// <summary>Source-generated pattern. A named group <c>secret</c>, when present, marks the value.</summary>
    public required Regex Pattern
    {
        get; init;
    }

    /// <summary>Confidence when the validator passes or there is none.</summary>
    public Confidence Confidence { get; init; } = Confidence.Medium;

    /// <summary>Optional validator over the matched value.</summary>
    public Func<string, bool>? Validator
    {
        get; init;
    }

    /// <summary>Confidence when the validator fails, or <see langword="null"/> to drop the match.</summary>
    public Confidence? ConfidenceWhenInvalid
    {
        get; init;
    }

    /// <summary>Minimum Shannon entropy of the value, in bits per character.</summary>
    public double MinimumEntropy
    {
        get; init;
    }

    /// <summary>
    /// Optional extent finder for multi-line structures: given the field text and the start of a header match, returns
    /// the end offset of the whole structure, or <see langword="null"/> when the structure is incomplete.
    /// </summary>
    public Func<string, int, int?>? Extent
    {
        get; init;
    }

    /// <summary>Fixed placement, overriding the default for the kind.</summary>
    public PlacementTarget? Target
    {
        get; init;
    }

    /// <summary>Whether the rule finds a public identifier rather than a secret.</summary>
    public bool IsIdentifier
    {
        get; init;
    }

    /// <summary>Upstream source: rule identifier and commit, or vendor documentation.</summary>
    public required string Source
    {
        get; init;
    }

    /// <summary>Whether the format was checked against the upstream source.</summary>
    public bool Verified { get; init; } = true;
}
