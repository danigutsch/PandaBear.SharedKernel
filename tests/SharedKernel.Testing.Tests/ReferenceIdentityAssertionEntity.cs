namespace PandaBear.Testing.Tests;

/// <summary>Class entity without equality overrides, so two instances are equal only by reference, used to exercise identity assertions.</summary>
internal sealed class ReferenceIdentityAssertionEntity
{
    /// <summary>Gets the identity, which stays <see cref="Guid.Empty" /> because the setter is private and never called.</summary>
    public Guid Id
    {
        get; private init;
    }
}
