namespace PandaBear.SecretScanning;

/// <summary>A finding while the scan runs, with the value it found, which <see cref="DetectedSecret"/> never holds.</summary>
internal sealed record Hit
{
    /// <summary>Gets the identifier of the rule or detector.</summary>
    public required string RuleId { get; init; }

    /// <summary>Gets the display name of the catalog rule, or <see langword="null"/> for a structural detector.</summary>
    public string? RuleName { get; init; }

    /// <summary>Gets what kind of secret it is.</summary>
    public required SecretKind Kind { get; init; }

    /// <summary>Gets how sure the scanner is.</summary>
    public required Confidence Confidence { get; init; }

    /// <summary>Gets where the value is.</summary>
    public required SecretLocation Location { get; init; }

    /// <summary>Gets where the label that names the value is, if any.</summary>
    public SecretLocation? LabelLocation { get; init; }

    /// <summary>Gets a value indicating whether a person should look at the finding first.</summary>
    public bool NeedsReview { get; init; }

    /// <summary>Gets a value indicating whether a pattern timed out on this region.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Gets a short description that never repeats the value.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the found value, or <see langword="null"/> when the finding spans text that is not one value.</summary>
    public string? Value { get; init; }

    /// <summary>Returns the finding without its value.</summary>
    /// <returns>The public finding.</returns>
    public DetectedSecret ToDetected() => new()
    {
        RuleId = RuleId,
        RuleName = RuleName,
        Kind = Kind,
        Confidence = Confidence,
        Location = Location,
        LabelLocation = LabelLocation,
        NeedsReview = NeedsReview,
        TimedOut = TimedOut,
        Message = Message,
    };

    /// <summary>Describes the finding without its value, so the value never reaches a log.</summary>
    /// <returns>The description of <see cref="ToDetected"/>.</returns>
    public override string ToString() => ToDetected().ToString();
}
