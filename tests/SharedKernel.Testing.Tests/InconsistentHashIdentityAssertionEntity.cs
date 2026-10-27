namespace PandaBear.Testing.Tests;

/// <summary>Entity that compares by <see cref="Id" /> but returns a caller-chosen hash code, so equal instances can hash differently to exercise hash-consistency assertions.</summary>
/// <param name="hashCode">The value returned from <see cref="GetHashCode" /> regardless of the identity.</param>
internal sealed class InconsistentHashIdentityAssertionEntity(int hashCode) : IEquatable<InconsistentHashIdentityAssertionEntity>
{
    /// <summary>Gets the identity used for equality, which stays <see cref="Guid.Empty" /> because the setter is private and never called.</summary>
    public Guid Id
    {
        get; private init;
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="other" /> is not null and has the same <see cref="Id" />.</summary>
    /// <param name="other">The entity to compare with.</param>
    /// <returns><see langword="true" /> when both entities share an identity.</returns>
    public bool Equals(InconsistentHashIdentityAssertionEntity? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => obj is InconsistentHashIdentityAssertionEntity other && Equals(other);

    public override int GetHashCode() => hashCode;
}
