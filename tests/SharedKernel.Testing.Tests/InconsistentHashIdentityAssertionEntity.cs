namespace PandaBear.Testing.Tests;

internal sealed class InconsistentHashIdentityAssertionEntity(int hashCode) : IEquatable<InconsistentHashIdentityAssertionEntity>
{
    public Guid Id
    {
        get; private init;
    }

    public bool Equals(InconsistentHashIdentityAssertionEntity? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => obj is InconsistentHashIdentityAssertionEntity other && Equals(other);

    public override int GetHashCode() => hashCode;
}
