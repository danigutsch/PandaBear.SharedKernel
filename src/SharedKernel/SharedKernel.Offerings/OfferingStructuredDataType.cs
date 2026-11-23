namespace PandaBear.Offerings;

/// <summary>The schema.org type <see cref="OfferingStructuredData"/> marks an offering up as.</summary>
public enum OfferingStructuredDataType
{
    /// <summary><c>TouristTrip</c>, with the provider and the itinerary.</summary>
    TouristTrip = 0,

    /// <summary><c>Product</c>, with the offer only.</summary>
    Product = 1,
}
