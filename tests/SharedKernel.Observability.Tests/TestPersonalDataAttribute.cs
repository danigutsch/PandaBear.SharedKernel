using Microsoft.Extensions.Compliance.Classification;

namespace SharedKernel.Observability.Tests;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
internal sealed class TestPersonalDataAttribute : DataClassificationAttribute
{
    public TestPersonalDataAttribute()
        : base(TestPrivacyTaxonomy.PersonalData)
    {
    }
}
