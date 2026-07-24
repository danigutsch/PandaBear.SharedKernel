using Microsoft.Extensions.Compliance.Classification;

namespace SharedKernel.Observability;

/// <summary>Classifies a logging parameter as credential data.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CredentialDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="CredentialDataAttribute"/> class.</summary>
    public CredentialDataAttribute()
        : base(PrivacyDataClassifications.Credential)
    {
    }
}
