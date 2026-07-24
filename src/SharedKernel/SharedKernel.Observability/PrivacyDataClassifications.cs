using Microsoft.Extensions.Compliance.Classification;

namespace SharedKernel.Observability;

/// <summary>
/// Defines technical classifications used to redact sensitive application telemetry.
/// </summary>
public static class PrivacyDataClassifications
{
    private const string TaxonomyName = "SharedKernel.Observability.Privacy";

    /// <summary>Gets the classification for data that identifies or can identify a person.</summary>
    public static DataClassification Personal { get; } = new(TaxonomyName, nameof(Personal));

    /// <summary>Gets the classification for high-risk personal or confidential data.</summary>
    public static DataClassification Sensitive { get; } = new(TaxonomyName, nameof(Sensitive));

    /// <summary>Gets the classification for credentials and authentication secrets.</summary>
    public static DataClassification Credential { get; } = new(TaxonomyName, nameof(Credential));

    /// <summary>Gets the classification for non-public financial and payment data.</summary>
    public static DataClassification Financial { get; } = new(TaxonomyName, nameof(Financial));
}
