namespace PandaBear.BuildingBlocks;

/// <summary>
/// The labels that name the parts of a <see cref="PostalAddress"/> in its validation messages, so that a message such as
/// <c>Street is required.</c> can use the label of the form field instead. Each label defaults to the English part name; set only those
/// that differ.
/// </summary>
public sealed class PostalAddressLabels
{
    /// <summary>Gets the English labels that <see cref="PostalAddress.Create"/> uses when no labels are given.</summary>
    public static PostalAddressLabels Default { get; } = new();

    /// <summary>Gets the label of the street and number; the default is <c>Street</c>.</summary>
    public string Street { get; init; } = "Street";

    /// <summary>Gets the label of the complement; the default is <c>Complement</c>.</summary>
    public string Complement { get; init; } = "Complement";

    /// <summary>Gets the label of the neighborhood; the default is <c>Neighborhood</c>.</summary>
    public string Neighborhood { get; init; } = "Neighborhood";

    /// <summary>Gets the label of the postal code; the default is <c>Postal code</c>.</summary>
    public string PostalCode { get; init; } = "Postal code";

    /// <summary>Gets the label of the city; the default is <c>City</c>.</summary>
    public string City { get; init; } = "City";

    /// <summary>Gets the label of the state or region; the default is <c>State</c>.</summary>
    public string State { get; init; } = "State";

    /// <summary>Gets the label of the country; the default is <c>Country</c>.</summary>
    public string Country { get; init; } = "Country";
}
