using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Testing.Assertions;

/// <summary>Verifies generated GUID identity equality, including empty identities.</summary>
public static class EntityIdentityShouldAssertionExtensions
{
    /// <summary>Verifies a UUID version 7 identifier.</summary>
    /// <param name="id">The identifier.</param>
    public static void ShouldBeUuidV7(this Guid id)
    {
        var guidText = id.ToString("D");
        guidText[14].ShouldBe('7');
    }

    private static void SetId<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(T entity, Guid id)
    {
        var idProperty = typeof(T).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var nonNullIdProperty = idProperty.ShouldNotBeNull();
        var idSetter = nonNullIdProperty.GetSetMethod(nonPublic: true);
        var nonNullIdSetter = idSetter.ShouldNotBeNull();

        nonNullIdSetter.Invoke(entity, [id]);
    }

    /// <summary>Verifies equality and hash behavior for matching, different, and empty GUID identities.</summary>
    /// <remarks>Mutates the supplied test-owned instances through their Id setter, including private init setters.</remarks>
    /// <typeparam name="T">The entity type with a GUID Id property.</typeparam>
    /// <param name="first">The first test-owned entity.</param>
    /// <param name="second">The test-owned entity assigned the same identity, then an empty identity.</param>
    /// <param name="different">The test-owned entity assigned a different identity.</param>
    public static void ShouldHaveGeneratedIdentitySemantics<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this T first, T second, T different)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(different);
        var id = Guid.CreateVersion7();
        SetId(first, id);
        SetId(second, id);
        SetId(different, Guid.CreateVersion7());
        var set = new HashSet<T> { first };

        (EqualityComparer<T>.Default.Equals(first, first)).ShouldBeTrue();
        (EqualityComparer<T>.Default.Equals(first, second)).ShouldBeTrue();
        (EqualityComparer<T>.Default.Equals(first, different)).ShouldBeFalse();
        var firstHashCode = first.GetHashCode();
        var secondHashCode = second.GetHashCode();
        secondHashCode.ShouldBe(firstHashCode);
        set.ShouldContain(second);
        set.ShouldNotContain(different);

        SetId(second, Guid.Empty);

        (EqualityComparer<T>.Default.Equals(first, second)).ShouldBeFalse();
        (EqualityComparer<T>.Default.Equals(second, first)).ShouldBeFalse();
        set.ShouldNotContain(second);
        (EqualityComparer<T>.Default.Equals(first, null)).ShouldBeFalse();
    }
}
