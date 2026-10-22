using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Payment card data.</summary>
internal sealed record CardData
{
    /// <summary>Name printed on the card.</summary>
    public string? CardholderName { get; init; }

    /// <summary>Card brand, for example Visa.</summary>
    public string? Brand { get; init; }

    /// <summary>Card number.</summary>
    public string? Number { get; init; }

    /// <summary>Expiration month.</summary>
    public string? ExpMonth { get; init; }

    /// <summary>Expiration year.</summary>
    public string? ExpYear { get; init; }

    /// <summary>Security code.</summary>
    public string? Code { get; init; }

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Brand = ").Append(Brand);
        return true;
    }
}
