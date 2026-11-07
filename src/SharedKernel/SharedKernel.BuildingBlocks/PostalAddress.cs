using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A postal address with sanitized parts; it is not geocoded or checked against a postal database.</summary>
public sealed class PostalAddress : ValueObject
{
    /// <summary>The maximum length of the street, complement, neighborhood, city and state parts.</summary>
    public const int MaxPartLength = 128;

    /// <summary>The maximum length of the postal code.</summary>
    public const int MaxPostalCodeLength = 32;

    private PostalAddress(string street, string? complement, string? neighborhood, string postalCode, string city, string? state, CountryCode country)
    {
        Street = street;
        Complement = complement;
        Neighborhood = neighborhood;
        PostalCode = postalCode;
        City = city;
        State = state;
        Country = country;
    }

    /// <summary>Gets the street and number. Personal data.</summary>
    public string Street
    {
        get;
    }

    /// <summary>Gets the optional complement such as an apartment.</summary>
    public string? Complement
    {
        get;
    }

    /// <summary>Gets the optional neighborhood or district.</summary>
    public string? Neighborhood
    {
        get;
    }

    /// <summary>Gets the postal code.</summary>
    public string PostalCode
    {
        get;
    }

    /// <summary>Gets the city.</summary>
    public string City
    {
        get;
    }

    /// <summary>Gets the optional state, province or region.</summary>
    public string? State
    {
        get;
    }

    /// <summary>Gets the country.</summary>
    public CountryCode Country
    {
        get;
    }

    /// <summary>Creates an address. Street, postal code, city and country are required; the rest are optional.</summary>
    /// <param name="street">The street and number.</param>
    /// <param name="complement">The optional complement.</param>
    /// <param name="neighborhood">The optional neighborhood.</param>
    /// <param name="postalCode">The postal code.</param>
    /// <param name="city">The city.</param>
    /// <param name="state">The optional state or region.</param>
    /// <param name="country">The ISO 3166-1 alpha-2 country code.</param>
    /// <returns>The address or validation errors for each invalid part.</returns>
    public static Result<PostalAddress> Create(
        string? street,
        string? complement,
        string? neighborhood,
        string? postalCode,
        string? city,
        string? state,
        string? country)
    {
        var errors = new ValidationErrors();
        var validStreet = TextRules.Required(errors, street, "street", "Street", MaxPartLength);
        var validComplement = TextRules.Optional(errors, complement, "complement", "Complement", MaxPartLength);
        var validNeighborhood = TextRules.Optional(errors, neighborhood, "neighborhood", "Neighborhood", MaxPartLength);
        var validPostalCode = TextRules.Required(errors, postalCode, "postalCode", "Postal code", MaxPostalCodeLength);
        var validCity = TextRules.Required(errors, city, "city", "City", MaxPartLength);
        var validState = TextRules.Optional(errors, state, "state", "State", MaxPartLength);
        var countryResult = CountryCode.Create(country);
        if (countryResult.IsFailure)
        {
            errors.Add(Result.Invalid(detail: "Country is invalid.", field: "country", message: "Country must be a valid ISO 3166-1 alpha-2 code."));
        }

        return errors.HasErrors
            ? errors.ToResult<PostalAddress>()
            : Result.Ok(new PostalAddress(validStreet, validComplement, validNeighborhood, validPostalCode, validCity, validState, countryResult.Value));
    }

    /// <inheritdoc />
    public override string ToString() => "PostalAddress [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return Complement;
        yield return Neighborhood;
        yield return PostalCode;
        yield return City;
        yield return State;
        yield return Country;
    }
}
