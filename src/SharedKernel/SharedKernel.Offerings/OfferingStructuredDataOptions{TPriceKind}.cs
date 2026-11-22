namespace PandaBear.Offerings;

/// <summary>What <see cref="OfferingStructuredData.Build{TImageId, TPriceKind, TMetric}"/> adds to the view's own data. Mark up only what the page shows.</summary>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
public sealed class OfferingStructuredDataOptions<TPriceKind>
    where TPriceKind : struct, Enum
{
    private readonly Uri? _canonicalUrl;
    private readonly IReadOnlyList<Uri> _imageUrls = [];

    /// <summary>Gets the schema.org type. The default is <see cref="OfferingStructuredDataType.TouristTrip"/>.</summary>
    public OfferingStructuredDataType Type { get; init; }

    /// <summary>Gets the absolute canonical address of the page, the <c>url</c> of the item and its offer.</summary>
    /// <exception cref="ArgumentException">The address is relative.</exception>
    public Uri? CanonicalUrl { get => _canonicalUrl; init => _canonicalUrl = AbsoluteUrl.Check(value, nameof(value)); }

    /// <summary>Gets the absolute addresses of the images the page shows, the cover first.</summary>
    /// <exception cref="ArgumentNullException">The list, or an address in it, is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An address is relative.</exception>
    public IReadOnlyList<Uri> ImageUrls
    {
        get => _imageUrls;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _imageUrls = [.. value.Select(url => AbsoluteUrl.Check(url, nameof(value)) ?? throw new ArgumentNullException(nameof(value)))];
        }
    }

    /// <summary>Gets the organization that provides the offering; a tourist trip's <c>provider</c>.</summary>
    public StructuredDataOrganization? Provider { get; init; }

    /// <summary>Gets the site-wide name of a price kind, for a price without its own label; a price's own label wins.</summary>
    public Func<TPriceKind, string?>? PriceLabel { get; init; }

    /// <summary>Gets an availability that replaces the offering's own in the offer, such as one derived from bookings.</summary>
    public OfferingAvailability? Availability { get; init; }
}
