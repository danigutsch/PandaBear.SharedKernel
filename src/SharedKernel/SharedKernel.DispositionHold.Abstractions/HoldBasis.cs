namespace PandaBear.DispositionHold;

/// <summary>
/// Names why a disposition hold restrains deletion or anonymization. The values are jurisdiction-neutral categories;
/// the package encodes no legal rule, retention period or entitlement.
/// </summary>
public enum HoldBasis
{
    /// <summary>The establishment, exercise or defence of a legal claim.</summary>
    LegalClaim = 0,

    /// <summary>A legal or regulatory obligation to keep the data.</summary>
    LegalObligation = 1,

    /// <summary>A restriction of processing requested by, or owed to, a data subject.</summary>
    DataSubjectRestriction = 2,

    /// <summary>An inquiry by a regulator or other authority.</summary>
    RegulatoryInquiry = 3,

    /// <summary>An internal or external investigation.</summary>
    Investigation = 4,
}
