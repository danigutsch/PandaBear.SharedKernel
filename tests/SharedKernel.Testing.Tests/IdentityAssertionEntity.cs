namespace PandaBear.Testing.Tests;

/// <summary>Record entity with value equality whose only member is a <see cref="Guid" /> identity, used to exercise identity assertions.</summary>
internal sealed record IdentityAssertionEntity
{
    /// <summary>Gets the identity, which stays <see cref="Guid.Empty" /> because the setter is private and never called.</summary>
    public Guid Id
    {
        get; private init;
    }
}
