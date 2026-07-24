using Microsoft.Extensions.Compliance.Classification;

namespace SharedKernel.Observability;

/// <summary>Classifies a logging parameter as sensitive data.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SensitiveDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="SensitiveDataAttribute"/> class.</summary>
    public SensitiveDataAttribute()
        : base(PrivacyDataClassifications.Sensitive)
    {
    }
}
