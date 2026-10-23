using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Personal identity data.</summary>
internal sealed record IdentityData
{
    /// <summary>Title, for example Mr or Dr.</summary>
    public string? Title
    {
        get; init;
    }

    /// <summary>First name.</summary>
    public string? FirstName
    {
        get; init;
    }

    /// <summary>Middle name.</summary>
    public string? MiddleName
    {
        get; init;
    }

    /// <summary>Last name.</summary>
    public string? LastName
    {
        get; init;
    }

    /// <summary>First address line.</summary>
    public string? Address1
    {
        get; init;
    }

    /// <summary>Second address line.</summary>
    public string? Address2
    {
        get; init;
    }

    /// <summary>Third address line.</summary>
    public string? Address3
    {
        get; init;
    }

    /// <summary>City or town.</summary>
    public string? City
    {
        get; init;
    }

    /// <summary>State or province.</summary>
    public string? State
    {
        get; init;
    }

    /// <summary>Postal code.</summary>
    public string? PostalCode
    {
        get; init;
    }

    /// <summary>Country.</summary>
    public string? Country
    {
        get; init;
    }

    /// <summary>Company.</summary>
    public string? Company
    {
        get; init;
    }

    /// <summary>Email address.</summary>
    public string? Email
    {
        get; init;
    }

    /// <summary>Phone number.</summary>
    public string? Phone
    {
        get; init;
    }

    /// <summary>Social security number.</summary>
    public string? Ssn
    {
        get; init;
    }

    /// <summary>User name.</summary>
    public string? Username
    {
        get; init;
    }

    /// <summary>Passport number.</summary>
    public string? PassportNumber
    {
        get; init;
    }

    /// <summary>Driver license number.</summary>
    public string? LicenseNumber
    {
        get; init;
    }

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Country = ").Append(Country);
        return true;
    }
}
