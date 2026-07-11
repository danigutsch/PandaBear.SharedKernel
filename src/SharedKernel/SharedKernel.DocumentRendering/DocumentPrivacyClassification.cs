namespace SharedKernel.DocumentRendering;

/// <summary>
/// Classifies content before it is included in a rendered document.
/// </summary>
public enum DocumentPrivacyClassification
{
    /// <summary>No classification was assigned.</summary>
    None = 0,

    /// <summary>Public customer-safe content.</summary>
    Public = 1,

    /// <summary>Operational content requiring normal staff handling.</summary>
    Operational = 2,

    /// <summary>Personal data.</summary>
    PersonalData = 3,

    /// <summary>Sensitive personal data.</summary>
    SensitivePersonalData = 4,
}
