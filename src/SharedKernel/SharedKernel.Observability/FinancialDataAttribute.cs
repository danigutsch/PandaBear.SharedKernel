using Microsoft.Extensions.Compliance.Classification;

namespace PandaBear.Observability;

/// <summary>Classifies a logging parameter as financial data.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FinancialDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="FinancialDataAttribute"/> class.</summary>
    public FinancialDataAttribute()
        : base(PrivacyDataClassifications.Financial)
    {
    }
}
