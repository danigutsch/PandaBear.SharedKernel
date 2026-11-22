using System.Text.Json.Nodes;

namespace PandaBear.Offerings;

/// <summary>
/// Builds schema.org JSON-LD for an offering's public page with <see cref="System.Text.Json.Nodes"/>, without reflection, so it works
/// under trimming and NativeAOT. Render it in a <c>&lt;script type="application/ld+json"&gt;</c> serialized with an encoder that escapes
/// <c>&lt;</c>, such as <c>JavaScriptEncoder.Default</c>, so the data cannot close the script.
/// </summary>
public static class OfferingStructuredData
{
    private const string Schema = "https://schema.org";

    /// <summary>
    /// Builds a <c>TouristTrip</c> (or <c>Product</c>) of what the page shows: the name, the description (the SEO description, else the
    /// summary), the address, the images, a tourist trip's provider, an offer at the from price with one <c>UnitPriceSpecification</c> per
    /// price (an <c>AggregateOffer</c> when several prices are of a base kind), and a tourist trip's itinerary as an <c>ItemList</c> of
    /// its steps. The availability is <c>InStock</c> when scheduled, <c>SoldOut</c>, <c>PreOrder</c> when the dates are to be confirmed,
    /// and <c>MadeToOrder</c> on request. Without a from price there is no offer. Departure and arrival times are left out, since an
    /// offering has days, not times.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="view">The public view the page shows.</param>
    /// <param name="options">The page's addresses, provider and price names.</param>
    /// <returns>The JSON-LD object.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static JsonObject Build<TImageId, TPriceKind, TMetric>(OfferingView<TImageId, TPriceKind, TMetric> view, OfferingStructuredDataOptions<TPriceKind> options)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(options);

        var trip = options.Type == OfferingStructuredDataType.TouristTrip;
        var json = new JsonObject
        {
            ["@context"] = Schema,
            ["@type"] = trip ? "TouristTrip" : "Product",
            ["name"] = view.Title,
        };
        if (view.SeoDescription is { } description)
        {
            json["description"] = description;
        }

        if (options.CanonicalUrl is { } url)
        {
            json["url"] = url.AbsoluteUri;
        }

        if (options.ImageUrls.Count > 0)
        {
            json["image"] = new JsonArray([.. options.ImageUrls.Select(image => (JsonNode?)image.AbsoluteUri)]);
        }

        if (trip && options.Provider is { } provider)
        {
            json["provider"] = Organization(provider);
        }

        if (Offer(view, options) is { } offer)
        {
            json["offers"] = offer;
        }

        if (trip && view.Steps.Count > 0)
        {
            json["itinerary"] = new JsonObject
            {
                ["@type"] = "ItemList",
                ["itemListElement"] = new JsonArray([.. view.Steps.Select(step => (JsonNode?)new JsonObject
                {
                    ["@type"] = "ListItem",
                    ["position"] = step.Number,
                    ["name"] = step.Title,
                })]),
            };
        }

        return json;
    }

    private static JsonObject Organization(StructuredDataOrganization provider)
    {
        var organization = new JsonObject { ["@type"] = "Organization", ["name"] = provider.Name };
        if (provider.Url is { } url)
        {
            organization["url"] = url.AbsoluteUri;
        }

        return organization;
    }

    private static JsonObject? Offer<TImageId, TPriceKind, TMetric>(OfferingView<TImageId, TPriceKind, TMetric> view, OfferingStructuredDataOptions<TPriceKind> options)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        if (view.FromPrice is not { } from)
        {
            return null;
        }

        var currency = from.Currency.Code;
        var bases = view.Prices.Where(price => price.IsBase).ToList();
        var offer = new JsonObject();
        if (bases.Count > 1)
        {
            offer["@type"] = "AggregateOffer";
            offer["lowPrice"] = from.Amount;
            offer["highPrice"] = bases.Max(price => price.Amount.Amount);
            offer["offerCount"] = bases.Count;
        }
        else
        {
            offer["@type"] = "Offer";
            offer["price"] = from.Amount;
        }

        offer["priceCurrency"] = currency;
        offer["availability"] = Schema + "/" + Availability(options.Availability ?? view.Availability);
        if (options.CanonicalUrl is { } url)
        {
            offer["url"] = url.AbsoluteUri;
        }

        offer["priceSpecification"] = new JsonArray([.. view.Prices.Select(price => (JsonNode?)Specification(price, currency, options))]);
        return offer;
    }

    private static JsonObject Specification<TPriceKind>(PriceView<TPriceKind> price, string currency, OfferingStructuredDataOptions<TPriceKind> options)
        where TPriceKind : struct, Enum
    {
        var specification = new JsonObject
        {
            ["@type"] = "UnitPriceSpecification",
            ["price"] = price.Amount.Amount,
            ["priceCurrency"] = currency,
        };
        if ((price.Label ?? options.PriceLabel?.Invoke(price.Kind)) is { } name)
        {
            specification["name"] = name;
        }

        return specification;
    }

    private static string Availability(OfferingAvailability availability) => availability switch
    {
        OfferingAvailability.SoldOut => "SoldOut",
        OfferingAvailability.DatesToBeConfirmed => "PreOrder",
        OfferingAvailability.OnRequest => "MadeToOrder",
        _ => "InStock",
    };
}
