using SharedKernel.InputNormalization;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

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
    public string Street { get; }

    /// <summary>Gets the optional complement such as an apartment.</summary>
    public string? Complement { get; }

    /// <summary>Gets the optional neighborhood or district.</summary>
    public string? Neighborhood { get; }

    /// <summary>Gets the postal code.</summary>
    public string PostalCode { get; }

    /// <summary>Gets the city.</summary>
    public string City { get; }

    /// <summary>Gets the optional state, province or region.</summary>
    public string? State { get; }

    /// <summary>Gets the country.</summary>
    public CountryCode Country { get; }

    /// <summary>Creates an address. Street, postal code, city and country are required; the rest are optional.</summary>
    /// <param name="street">The street and number.</param>
    /// <param name="complement">The optional complement.</param>
    /// <param name="neighborhood">The optional neighborhood.</param>
    /// <param name="postalCode">The postal code.</param>
    /// <param name="city">The city.</param>
    /// <param name="state">The optional state or region.</param>
    /// <param name="country">The ISO 3166-1 alpha-2 country code.</param>
    /// <param name="labels">
    /// The labels that start the validation messages of each part, such as the form labels; <see langword="null"/> uses
    /// <see cref="PostalAddressLabels.Default"/>. The field keys (<c>street</c>, <c>postalCode</c> and so on) do not change.
    /// </param>
    /// <returns>The address or validation errors for each invalid part.</returns>
    public static Result<PostalAddress> Create(
        string? street,
        string? complement,
        string? neighborhood,
        string? postalCode,
        string? city,
        string? state,
        string? country,
        PostalAddressLabels? labels = null)
    {
        labels ??= PostalAddressLabels.Default;
        var errors = new ValidationErrors();
        var validStreet = SanitizedText.Required(errors, street, "street", labels.Street, MaxPartLength);
        var validComplement = SanitizedText.Optional(errors, complement, "complement", labels.Complement, MaxPartLength);
        var validNeighborhood = SanitizedText.Optional(errors, neighborhood, "neighborhood", labels.Neighborhood, MaxPartLength);
        var validPostalCode = SanitizedText.Required(errors, postalCode, "postalCode", labels.PostalCode, MaxPostalCodeLength);
        var validCity = SanitizedText.Required(errors, city, "city", labels.City, MaxPartLength);
        var validState = SanitizedText.Optional(errors, state, "state", labels.State, MaxPartLength);
        var countryResult = CountryCode.Create(country);
        if (countryResult.IsFailure)
        {
            errors.Add(Result.Invalid(
                detail: $"{labels.Country} is invalid.",
                field: "country",
                message: $"{labels.Country} must be a valid ISO 3166-1 alpha-2 code."));
        }

        return errors.HasErrors
            ? errors.ToResult<PostalAddress>()
            : Result.Ok(new PostalAddress(validStreet!, validComplement, validNeighborhood, validPostalCode!, validCity!, validState, countryResult.Value));
    }

    /// <summary>For persistence mappers: rebuilds an address from parts that were produced by this type earlier; it performs NO validation beyond null/blank checks of the required parts and the same sanitizing as <see cref="Create"/>, so length limits are not enforced and a stored value that a newer rule would reject can still be loaded; never use it for user input.</summary>
    /// <param name="street">The stored street and number.</param>
    /// <param name="complement">The stored complement; blank becomes <see langword="null"/>.</param>
    /// <param name="neighborhood">The stored neighborhood; blank becomes <see langword="null"/>.</param>
    /// <param name="postalCode">The stored postal code.</param>
    /// <param name="city">The stored city.</param>
    /// <param name="state">The stored state or region; blank becomes <see langword="null"/>.</param>
    /// <param name="country">The stored country.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentNullException">A required part or <paramref name="country"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required text part is empty or blank.</exception>
    public static PostalAddress Restore(
        string street,
        string? complement,
        string? neighborhood,
        string postalCode,
        string city,
        string? state,
        CountryCode country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentNullException.ThrowIfNull(country);
        var validStreet = StringSanitizer.Sanitize(street);
        var validPostalCode = StringSanitizer.Sanitize(postalCode);
        var validCity = StringSanitizer.Sanitize(city);
        ArgumentException.ThrowIfNullOrEmpty(validStreet, nameof(street));
        ArgumentException.ThrowIfNullOrEmpty(validPostalCode, nameof(postalCode));
        ArgumentException.ThrowIfNullOrEmpty(validCity, nameof(city));
        return new PostalAddress(
            validStreet,
            NullIfEmpty(StringSanitizer.Sanitize(complement)),
            NullIfEmpty(StringSanitizer.Sanitize(neighborhood)),
            validPostalCode,
            validCity,
            NullIfEmpty(StringSanitizer.Sanitize(state)),
            country);
    }

    /// <inheritdoc />
    public override string ToString() => "PostalAddress [redacted]";

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

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
