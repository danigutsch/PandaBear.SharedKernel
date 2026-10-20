using Microsoft.Extensions.Compliance.Classification;

namespace PandaBear.Observability;

/// <summary>Classifies a logging parameter as personal data.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="PersonalDataAttribute"/> class.</summary>
    public PersonalDataAttribute()
        : base(PrivacyDataClassifications.Personal)
    {
    }
}
