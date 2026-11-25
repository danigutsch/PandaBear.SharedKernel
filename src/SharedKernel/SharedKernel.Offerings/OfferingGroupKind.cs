namespace SharedKernel.Offerings;

/// <summary>The kind of a group of the offering listing (see <see cref="OfferingListing"/>).</summary>
public enum OfferingGroupKind
{
    /// <summary>The scheduled, sold-out and to-be-confirmed offerings of one year.</summary>
    Year = 0,

    /// <summary>The offerings arranged on request.</summary>
    OnRequest = 1,
}
