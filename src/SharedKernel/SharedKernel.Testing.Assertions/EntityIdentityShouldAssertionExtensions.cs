using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using static PandaBear.Testing.Assertions.UserMessageAssertions;

namespace PandaBear.Testing.Assertions;

/// <summary>Verifies generated GUID identity equality, including empty identities.</summary>
public static class EntityIdentityShouldAssertionExtensions
{
    /// <summary>Verifies a UUID version 7 identifier with the standard UUID variant.</summary>
    /// <param name="id">The identifier.</param>
    public static void ShouldBeUuidV7(this Guid id)
    {
        var guidText = id.ToString("D");
        guidText[14].ShouldBe('7');
        var hasStandardVariant = guidText[19] is '8' or '9' or 'a' or 'b';
        hasStandardVariant.ShouldBeTrue("Expected the standard UUID variant.");
    }

    /// <summary>Verifies a UUID version 7 identifier with the standard UUID variant.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeUuidV7(this Guid id, string userMessage) =>
        WithUserMessage(userMessage, () => id.ShouldBeUuidV7());

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="first"/>, <paramref name="second"/>, or <paramref name="different"/> is null.</exception>
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

    /// <summary>Verifies equality and hash behavior for matching, different, and empty GUID identities.</summary>
    /// <remarks>Mutates the supplied test-owned instances through their Id setter, including private init setters.</remarks>
    /// <typeparam name="T">The entity type with a GUID Id property.</typeparam>
    /// <param name="first">The first test-owned entity.</param>
    /// <param name="second">The test-owned entity assigned the same identity, then an empty identity.</param>
    /// <param name="different">The test-owned entity assigned a different identity.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="first"/>, <paramref name="second"/>, or <paramref name="different"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldHaveGeneratedIdentitySemantics<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this T first, T second, T different, string userMessage)
        where T : class =>
        WithUserMessage(userMessage, () => first.ShouldHaveGeneratedIdentitySemantics(second, different));
}
