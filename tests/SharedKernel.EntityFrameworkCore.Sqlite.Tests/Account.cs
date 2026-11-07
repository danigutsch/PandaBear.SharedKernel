namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>An entity with a tagged single-column unique index, a tagged composite one, and an untagged one.</summary>
public sealed class Account
{
    /// <summary>Gets or sets the key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the tenant, the first part of the composite unique index.</summary>
    public int TenantId
    {
        get; set;
    }

    /// <summary>Gets or sets the code, the second part of the composite unique index.</summary>
    public string Code { get; set; } = "code";

    /// <summary>Gets or sets the email, covered by a tagged unique index.</summary>
    public string Email { get; set; } = "a@example.com";

    /// <summary>Gets or sets the nickname, covered by an untagged unique index.</summary>
    public string? Nickname
    {
        get; set;
    }

    /// <summary>Gets or sets the required name.</summary>
    public string? Name { get; set; } = "name";
}
