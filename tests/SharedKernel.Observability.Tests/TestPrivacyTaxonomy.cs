using Microsoft.Extensions.Compliance.Classification;

namespace SharedKernel.Observability.Tests;

internal static class TestPrivacyTaxonomy
{
    public static readonly DataClassification PersonalData = new(nameof(TestPrivacyTaxonomy), nameof(PersonalData));
}
