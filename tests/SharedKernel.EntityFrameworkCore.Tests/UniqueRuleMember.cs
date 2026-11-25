namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>A row with a rule-tagged unique email and an untagged unique nickname.</summary>
public sealed class UniqueRuleMember
{
    /// <summary>Gets or sets the key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the email, covered by a tagged unique index.</summary>
    public string Email { get; set; } = "a@example.com";

    /// <summary>Gets or sets the nickname, covered by an untagged unique index.</summary>
    public string Nickname { get; set; } = Guid.NewGuid().ToString("N");
}
