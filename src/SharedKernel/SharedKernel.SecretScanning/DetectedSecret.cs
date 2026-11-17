using System.Globalization;

namespace PandaBear.SecretScanning;

/// <summary>A secret that <see cref="SecretScanner"/> found: what it is, how sure the scanner is, and where it is, never the value.</summary>
public sealed record DetectedSecret
{
    /// <summary>Gets an identifier that is stable for the same rule at the same place.</summary>
    public string Id => string.Create(CultureInfo.InvariantCulture, $"{RuleId}@{Location.FieldPath}:{Location.Start}");

    /// <summary>Gets the identifier of the rule or detector that found the secret, such as <c>aws-access-key</c>.</summary>
    public required string RuleId { get; init; }

    /// <summary>Gets the display name of the catalog rule that found the secret, or <see langword="null"/> for a structural detector.</summary>
    public string? RuleName { get; init; }

    /// <summary>Gets what kind of secret it is.</summary>
    public required SecretKind Kind { get; init; }

    /// <summary>Gets how sure the scanner is.</summary>
    public required Confidence Confidence { get; init; }

    /// <summary>Gets where the value is.</summary>
    public required SecretLocation Location { get; init; }

    /// <summary>Gets where the label that names the value is, such as the key of a <c>password: ...</c> line, if any.</summary>
    public SecretLocation? LabelLocation { get; init; }

    /// <summary>Gets a value indicating whether a person should look at the finding before anyone acts on it.</summary>
    public bool NeedsReview { get; init; }

    /// <summary>Gets a value indicating whether a pattern timed out on this region, so it may hide a secret the scanner could not judge.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Gets a short description, which never repeats the value.</summary>
    public required string Message { get; init; }

    /// <summary>Describes the finding without its value.</summary>
    /// <returns>The identifier, kind, confidence, length, and message.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Id} {Kind} {Confidence} length {Location.Length}{(NeedsReview ? " (needs review)" : string.Empty)}: {Message}");
}
