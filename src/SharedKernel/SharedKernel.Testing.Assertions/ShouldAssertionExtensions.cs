using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PandaBear.Testing.Assertions.UserMessageAssertions;

namespace PandaBear.Testing.Assertions;

/// <summary>
/// Provides assertion helpers for values used in tests.
/// </summary>
public static class ShouldAssertionExtensions
{
    /// <summary>
    /// Verifies equality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public static void ShouldBe<T>(this T actual, T expected) => Xunit.Assert.Equal(expected, actual);

    /// <summary>
    /// Verifies equality with the expected value and states why the test expects it when the values differ.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="userMessage">Why the test expects the value; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBe<T>(this T actual, T expected, string userMessage) =>
        WithUserMessage(userMessage, () => Xunit.Assert.Equal(expected, actual));

    /// <summary>
    /// Verifies equality with a nullable expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public static void ShouldBe<T>(this T actual, T? expected)
        where T : struct => Xunit.Assert.Equal(expected, actual);

    /// <summary>
    /// Verifies equality with a nullable expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBe<T>(this T actual, T? expected, string userMessage)
        where T : struct =>
        WithUserMessage(userMessage, () => actual.ShouldBe(expected));

    /// <summary>
    /// Verifies equality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldBe<T>(this T actual, T expected, IEqualityComparer<T> comparer) => Xunit.Assert.Equal(expected, actual, comparer);

    /// <summary>
    /// Verifies equality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBe<T>(this T actual, T expected, IEqualityComparer<T> comparer, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBe(expected, comparer));

    /// <summary>
    /// Verifies floating-point equality with precision.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="precision">The precision.</param>
    public static void ShouldBe(this double actual, double expected, int precision) => Xunit.Assert.Equal(expected, actual, precision);

    /// <summary>
    /// Verifies floating-point equality with precision.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="precision">The precision.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBe(this double actual, double expected, int precision, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBe(expected, precision));

    /// <summary>
    /// Verifies floating-point equality with precision and midpoint rounding.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="precision">The precision.</param>
    /// <param name="rounding">The midpoint rounding behavior.</param>
    public static void ShouldBe(this double actual, double expected, int precision, MidpointRounding rounding) =>
        Xunit.Assert.Equal(expected, actual, precision, rounding);

    /// <summary>
    /// Verifies floating-point equality with precision and midpoint rounding.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="precision">The precision.</param>
    /// <param name="rounding">The midpoint rounding behavior.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBe(this double actual, double expected, int precision, MidpointRounding rounding, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBe(expected, precision, rounding));

    /// <summary>
    /// Verifies strict collection equivalence without requiring the same item order.
    /// </summary>
    /// <typeparam name="T">The collection item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected items, including duplicate multiplicity.</param>
    public static void ShouldBeEquivalentTo<T>(this IEnumerable<T> actual, params T[] expected) =>
        Xunit.Assert.Equivalent(expected, actual, strict: true);

    /// <summary>
    /// Verifies strict collection equivalence without requiring the same item order.
    /// </summary>
    /// <typeparam name="T">The collection item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected items, including duplicate multiplicity.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeEquivalentTo<T>(this IEnumerable<T> actual, T[] expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeEquivalentTo(expected));

    /// <summary>
    /// Verifies inequality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public static void ShouldNotBe<T>(this T actual, T expected) => Xunit.Assert.NotEqual(expected, actual);

    /// <summary>
    /// Verifies inequality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotBe<T>(this T actual, T expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotBe(expected));

    /// <summary>
    /// Verifies inequality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldNotBe<T>(this T actual, T expected, IEqualityComparer<T> comparer) => Xunit.Assert.NotEqual(expected, actual, comparer);

    /// <summary>
    /// Verifies inequality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotBe<T>(this T actual, T expected, IEqualityComparer<T> comparer, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotBe(expected, comparer));

    /// <summary>
    /// Verifies reference equality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected reference.</param>
    public static void ShouldBeSameAs<T>(this T? actual, T? expected)
        where T : class => Xunit.Assert.Same(expected, actual);

    /// <summary>
    /// Verifies reference equality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected reference.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeSameAs<T>(this T? actual, T? expected, string userMessage)
        where T : class =>
        WithUserMessage(userMessage, () => actual.ShouldBeSameAs(expected));

    /// <summary>
    /// Verifies reference inequality with the unexpected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="unexpected">The unexpected reference.</param>
    public static void ShouldNotBeSameAs<T>(this T? actual, T? unexpected)
        where T : class => Xunit.Assert.NotSame(unexpected, actual);

    /// <summary>
    /// Verifies reference inequality with the unexpected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="unexpected">The unexpected reference.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotBeSameAs<T>(this T? actual, T? unexpected, string userMessage)
        where T : class =>
        WithUserMessage(userMessage, () => actual.ShouldNotBeSameAs(unexpected));

    /// <summary>
    /// Verifies type equality.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeOfType<T>(this object? actual) => Xunit.Assert.IsType<T>(actual);

    /// <summary>
    /// Verifies type equality.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The typed value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldBeOfType<T>(this object? actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOfType<T>());

    /// <summary>
    /// Verifies type equality, optionally allowing derived types.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="exactMatch">Whether the type must match exactly.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeOfType<T>(this object? actual, bool exactMatch) => Xunit.Assert.IsType<T>(actual, exactMatch);

    /// <summary>
    /// Verifies type equality, optionally allowing derived types.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="exactMatch">Whether the type must match exactly.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The typed value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldBeOfType<T>(this object? actual, bool exactMatch, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOfType<T>(exactMatch));

    /// <summary>
    /// Verifies type inequality.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    public static void ShouldNotBeOfType<T>(this object? actual) => Xunit.Assert.IsNotType<T>(actual);

    /// <summary>
    /// Verifies type inequality.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotBeOfType<T>(this object? actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotBeOfType<T>());

    /// <summary>
    /// Verifies assignability.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeAssignableTo<T>(this object? actual) => Xunit.Assert.IsAssignableFrom<T>(actual);

    /// <summary>
    /// Verifies assignability.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The typed value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldBeAssignableTo<T>(this object? actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeAssignableTo<T>());

    /// <summary>
    /// Verifies non-assignability.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    public static void ShouldNotBeAssignableTo<T>(this object? actual) => Xunit.Assert.IsNotAssignableFrom<T>(actual);

    /// <summary>
    /// Verifies non-assignability.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotBeAssignableTo<T>(this object? actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotBeAssignableTo<T>());

    /// <summary>
    /// Verifies that a nullable reference is not null and updates compiler null-state flow.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The non-null value.</returns>
    public static T ShouldNotBeNull<T>([NotNull] this T? actual)
        where T : class
    {
        Xunit.Assert.NotNull(actual);
        return actual;
    }

    /// <summary>
    /// Verifies that a nullable reference is not null and updates compiler null-state flow.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The non-null value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldNotBeNull<T>([NotNull] this T? actual, string userMessage)
        where T : class
    {
        // Assigning the checked value gives the parameter the non-null state that [NotNull] promises the caller.
        actual = WithUserMessage(userMessage, () => actual.ShouldNotBeNull());
        return actual;
    }

    /// <summary>
    /// Verifies that a nullable value type is not null.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The non-null value.</returns>
    public static T ShouldNotBeNull<T>([NotNull] this T? actual)
        where T : struct
    {
        Xunit.Assert.NotNull(actual);
        return actual.Value;
    }

    /// <summary>
    /// Verifies that a nullable value type is not null.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The non-null value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldNotBeNull<T>([NotNull] this T? actual, string userMessage)
        where T : struct
    {
        // Assigning the checked value gives the parameter the non-null state that [NotNull] promises the caller.
        actual = WithUserMessage(userMessage, () => actual.ShouldNotBeNull());
        return actual.Value;
    }

    /// <summary>
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    public static void ShouldBeNull(this object? actual) => Xunit.Assert.Null(actual);

    /// <summary>
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeNull(this object? actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeNull());

    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeTrue(this bool actual, string? userMessage = null) => Xunit.Assert.True(actual, userMessage);

    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeTrue(this bool? actual, string? userMessage = null) => Xunit.Assert.True(actual, userMessage);

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeFalse(this bool actual, string? userMessage = null) => Xunit.Assert.False(actual, userMessage);

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeFalse(this bool? actual, string? userMessage = null) => Xunit.Assert.False(actual, userMessage);

    /// <summary>
    /// Verifies that a string contains the expected fragment with ordinal comparison.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    public static void ShouldContain(this string? actual, string expected) => Xunit.Assert.Contains(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string contains the expected fragment with ordinal comparison.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldContain(this string? actual, string expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldContain(expected, StringComparison.Ordinal));

    /// <summary>
    /// Verifies that a string contains the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldContain(this string? actual, string expected, StringComparison comparisonType) => Xunit.Assert.Contains(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a string contains the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldContain(this string? actual, string expected, StringComparison comparisonType, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldContain(expected, comparisonType));

    /// <summary>
    /// Verifies that a collection contains the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected) => Xunit.Assert.Contains(expected, actual);

    /// <summary>
    /// Verifies that a collection contains the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldContain(expected));

    /// <summary>
    /// Verifies that a collection contains the expected item using a comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T> comparer) => Xunit.Assert.Contains(expected, actual, comparer);

    /// <summary>
    /// Verifies that a collection contains the expected item using a comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="comparer">The equality comparer.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T> comparer, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldContain(expected, comparer));

    /// <summary>
    /// Verifies that a collection contains a matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, Predicate<T> predicate) => Xunit.Assert.Contains(actual, predicate);

    /// <summary>
    /// Verifies that a collection contains a matching item and states why the test expects one when none matches.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    /// <param name="userMessage">Why the test expects a matching item; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldContain<T>(this IEnumerable<T> actual, Predicate<T> predicate, string userMessage) =>
        WithUserMessage(userMessage, () => Xunit.Assert.Contains(actual, predicate));

    /// <summary>
    /// Verifies that a value equals one of the expected values.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The value being verified.</param>
    /// <param name="expected">The accepted values.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null.</exception>
    public static void ShouldBeOneOf<T>(this T actual, params T[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Xunit.Assert.Contains(actual, expected);
    }

    /// <summary>
    /// Verifies that a value equals one of the expected values.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The value being verified.</param>
    /// <param name="expected">The accepted values.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeOneOf<T>(this T actual, T[] expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOneOf(expected));

    /// <summary>
    /// Verifies that a string contains every expected fragment in ordinal order.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragments.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> or one of its fragments is null.</exception>
    public static void ShouldContainInOrder(this string? actual, params string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        foreach (var fragment in expected)
        {
            if (fragment is null)
            {
                throw new ArgumentNullException(nameof(expected), "Expected fragments cannot contain null values.");
            }
        }

        Xunit.Assert.NotNull(actual);

        var startIndex = 0;
        foreach (var fragment in expected)
        {
            var index = actual.IndexOf(fragment, startIndex, StringComparison.Ordinal);
            Xunit.Assert.True(index >= 0, $"Expected string to contain '{fragment}' after the prior fragment.");
            startIndex = index + fragment.Length;
        }
    }

    /// <summary>
    /// Verifies that a string contains every expected fragment in ordinal order.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragments.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> or one of its fragments is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldContainInOrder(this string? actual, string[] expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldContainInOrder(expected));

    /// <summary>
    /// Verifies that a rendered HTML document is complete and XML-compatible.
    /// </summary>
    /// <param name="actual">The rendered HTML document.</param>
    public static void ShouldBeWellFormedHtmlDocument(this string? actual)
    {
        Xunit.Assert.NotNull(actual);
        var document = XDocument.Parse(actual);
        Xunit.Assert.Equal("html", document.DocumentType?.Name);
        Xunit.Assert.Equal("html", document.Root?.Name.LocalName);
        Xunit.Assert.NotNull(document.Root?.Element("head"));
        Xunit.Assert.NotNull(document.Root?.Element("body"));
    }

    /// <summary>
    /// Verifies that a rendered HTML document is complete and XML-compatible.
    /// </summary>
    /// <param name="actual">The rendered HTML document.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeWellFormedHtmlDocument(this string? actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeWellFormedHtmlDocument());

    /// <summary>
    /// Verifies that a string ends with the expected suffix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldEndWith(this string? actual, string expected, StringComparison comparisonType) => Xunit.Assert.EndsWith(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a string ends with the expected suffix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldEndWith(this string? actual, string expected, StringComparison comparisonType, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldEndWith(expected, comparisonType));

    /// <summary>
    /// Verifies that a string ends with the expected suffix using ordinal comparison.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    public static void ShouldEndWith(this string? actual, string expected) => Xunit.Assert.EndsWith(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string ends with the expected suffix using ordinal comparison.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldEndWith(this string? actual, string expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldEndWith(expected, StringComparison.Ordinal));

    /// <summary>
    /// Verifies that a collection does not contain the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, T expected) => Xunit.Assert.DoesNotContain(expected, actual);

    /// <summary>
    /// Verifies that a collection does not contain the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, T expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotContain(expected));

    /// <summary>
    /// Verifies that a collection does not contain the expected item using a comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T> comparer) =>
        Xunit.Assert.DoesNotContain(expected, actual, comparer);

    /// <summary>
    /// Verifies that a collection does not contain the expected item using a comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="comparer">The equality comparer.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T> comparer, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotContain(expected, comparer));

    /// <summary>
    /// Verifies that a collection does not contain a matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, Predicate<T> predicate) => Xunit.Assert.DoesNotContain(actual, predicate);

    /// <summary>
    /// Verifies that a collection does not contain a matching item and states why the test expects none when one
    /// matches.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    /// <param name="userMessage">Why the test expects no matching item; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, Predicate<T> predicate, string userMessage) =>
        WithUserMessage(userMessage, () => Xunit.Assert.DoesNotContain(actual, predicate));

    /// <summary>
    /// Verifies that a string does not contain the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    public static void ShouldNotContain(this string? actual, string expected) => Xunit.Assert.DoesNotContain(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string does not contain the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotContain(this string? actual, string expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotContain(expected, StringComparison.Ordinal));

    /// <summary>
    /// Verifies that a string does not contain the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldNotContain(this string? actual, string expected, StringComparison comparisonType) => Xunit.Assert.DoesNotContain(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a string does not contain the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotContain(this string? actual, string expected, StringComparison comparisonType, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotContain(expected, comparisonType));

    /// <summary>
    /// Verifies that a collection is empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    public static void ShouldBeEmpty<T>(this IEnumerable<T> actual) => Xunit.Assert.Empty(actual);

    /// <summary>
    /// Verifies that a collection is empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeEmpty<T>(this IEnumerable<T> actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeEmpty());

    /// <summary>
    /// Verifies that a collection is not empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    public static void ShouldNotBeEmpty<T>(this IEnumerable<T> actual) => Xunit.Assert.NotEmpty(actual);

    /// <summary>
    /// Verifies that a collection is not empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotBeEmpty<T>(this IEnumerable<T> actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotBeEmpty());

    /// <summary>
    /// Verifies that a collection contains the expected number of items.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expectedCount">The expected item count.</param>
    public static void ShouldHaveCount<T>(this IEnumerable<T> actual, int expectedCount) => Xunit.Assert.Equal(expectedCount, actual.Count());

    /// <summary>
    /// Verifies that a collection contains the expected number of items.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expectedCount">The expected item count.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldHaveCount<T>(this IEnumerable<T> actual, int expectedCount, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveCount(expectedCount));

    /// <summary>
    /// Verifies that a collection contains exactly one item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <returns>The single item.</returns>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual) => Xunit.Assert.Single(actual);

    /// <summary>
    /// Verifies that a collection contains exactly one item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The single item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveSingleItem());

    /// <summary>
    /// Verifies that a collection contains exactly one matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    /// <returns>The single matching item.</returns>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual, Predicate<T> predicate) =>
        Xunit.Assert.Single(actual.Where(item => predicate(item)));

    /// <summary>
    /// Verifies that a collection contains exactly one matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The single matching item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual, Predicate<T> predicate, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveSingleItem(predicate));

    /// <summary>
    /// Verifies that a value is within a range.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    public static void ShouldBeInRange<T>(this T actual, T low, T high)
        where T : IComparable => Xunit.Assert.InRange(actual, low, high);

    /// <summary>
    /// Verifies that a value is within a range.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeInRange<T>(this T actual, T low, T high, string userMessage)
        where T : IComparable =>
        WithUserMessage(userMessage, () => actual.ShouldBeInRange(low, high));

    /// <summary>
    /// Verifies that a value is greater than the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The exclusive lower bound.</param>
    public static void ShouldBeGreaterThan<T>(this T actual, T expected)
        where T : IComparable<T> => (actual.CompareTo(expected) > 0).ShouldBeTrue(
            $"Expected value greater than {expected}, but found {actual}.");

    /// <summary>
    /// Verifies that a value is greater than the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The exclusive lower bound.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeGreaterThan<T>(this T actual, T expected, string userMessage)
        where T : IComparable<T> =>
        WithUserMessage(userMessage, () => actual.ShouldBeGreaterThan(expected));

    /// <summary>
    /// Verifies that a value is greater than or equal to the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The inclusive lower bound.</param>
    public static void ShouldBeGreaterThanOrEqualTo<T>(this T actual, T expected)
        where T : IComparable<T> => (actual.CompareTo(expected) >= 0).ShouldBeTrue(
            $"Expected value greater than or equal to {expected}, but found {actual}.");

    /// <summary>
    /// Verifies that a value is greater than or equal to the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The inclusive lower bound.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeGreaterThanOrEqualTo<T>(this T actual, T expected, string userMessage)
        where T : IComparable<T> =>
        WithUserMessage(userMessage, () => actual.ShouldBeGreaterThanOrEqualTo(expected));

    /// <summary>
    /// Verifies that a value is less than the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The exclusive upper bound.</param>
    public static void ShouldBeLessThan<T>(this T actual, T expected)
        where T : IComparable<T> => (actual.CompareTo(expected) < 0).ShouldBeTrue(
            $"Expected value less than {expected}, but found {actual}.");

    /// <summary>
    /// Verifies that a value is less than the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The exclusive upper bound.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeLessThan<T>(this T actual, T expected, string userMessage)
        where T : IComparable<T> =>
        WithUserMessage(userMessage, () => actual.ShouldBeLessThan(expected));

    /// <summary>
    /// Verifies that a value is less than or equal to the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The inclusive upper bound.</param>
    public static void ShouldBeLessThanOrEqualTo<T>(this T actual, T expected)
        where T : IComparable<T> => (actual.CompareTo(expected) <= 0).ShouldBeTrue(
            $"Expected value less than or equal to {expected}, but found {actual}.");

    /// <summary>
    /// Verifies that a value is less than or equal to the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The inclusive upper bound.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeLessThanOrEqualTo<T>(this T actual, T expected, string userMessage)
        where T : IComparable<T> =>
        WithUserMessage(userMessage, () => actual.ShouldBeLessThanOrEqualTo(expected));

    /// <summary>
    /// Verifies that a string starts with the expected value.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected start value.</param>
    public static void ShouldStartWith(this string? actual, string expected) => Xunit.Assert.StartsWith(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string starts with the expected value.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected start value.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldStartWith(this string? actual, string expected, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldStartWith(expected, StringComparison.Ordinal));

    /// <summary>
    /// Verifies that a string starts with the expected prefix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected prefix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldStartWith(this string? actual, string expected, StringComparison comparisonType) =>
        Xunit.Assert.StartsWith(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a string starts with the expected prefix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected prefix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldStartWith(this string? actual, string expected, StringComparison comparisonType, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldStartWith(expected, comparisonType));

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegexPattern">The expected regular expression pattern.</param>
    public static void ShouldMatch(this string? actual, string expectedRegexPattern) => Xunit.Assert.Matches(expectedRegexPattern, actual);

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegexPattern">The expected regular expression pattern.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldMatch(this string? actual, string expectedRegexPattern, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldMatch(expectedRegexPattern));

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegex">The expected regular expression.</param>
    public static void ShouldMatch(this string? actual, Regex expectedRegex) => Xunit.Assert.Matches(expectedRegex, actual);

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegex">The expected regular expression.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldMatch(this string? actual, Regex expectedRegex, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldMatch(expectedRegex));

    /// <summary>
    /// Verifies that a string does not match a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegex">The unexpected regular expression.</param>
    public static void ShouldNotMatch(this string? actual, Regex expectedRegex) => Xunit.Assert.DoesNotMatch(expectedRegex, actual);

    /// <summary>
    /// Verifies that a string does not match a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegex">The unexpected regular expression.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldNotMatch(this string? actual, Regex expectedRegex, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldNotMatch(expectedRegex));

    /// <summary>
    /// Verifies every item in a collection.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="action">The inspector.</param>
    public static void ShouldAllSatisfy<T>(this IEnumerable<T> actual, Action<T> action) => Xunit.Assert.All(actual, action);

    /// <summary>
    /// Verifies every item in a collection.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="action">The inspector.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldAllSatisfy<T>(this IEnumerable<T> actual, Action<T> action, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldAllSatisfy(action));

    /// <summary>
    /// Verifies collection items with ordered inspectors.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="inspectors">The ordered item inspectors.</param>
    public static void ShouldMatchCollection<T>(this IEnumerable<T> actual, params Action<T>[] inspectors) =>
        Xunit.Assert.Collection(actual, inspectors);

    /// <summary>
    /// Verifies collection items with ordered inspectors.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="inspectors">The ordered item inspectors.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldMatchCollection<T>(this IEnumerable<T> actual, Action<T>[] inspectors, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldMatchCollection(inspectors));

    /// <summary>
    /// Verifies that an action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The thrown exception.</returns>
    [SuppressMessage("Testing", "SKTEST017:Public assertions should have a userMessage overload", Justification = "ShouldThrow<T>(Action, string? paramName) already uses the (Action, string) signature, so a userMessage overload would not compile (CS0111); argument exceptions take a message through ShouldThrow<T>(paramName, userMessage).")]
    public static T ShouldThrow<T>(this Action action)
        where T : Exception => Xunit.Assert.Throws<T>(action);

    /// <summary>
    /// Verifies that an action throws the expected argument exception parameter.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <param name="paramName">The expected parameter name.</param>
    /// <returns>The thrown exception.</returns>
    public static T ShouldThrow<T>(this Action action, string? paramName)
        where T : ArgumentException => Xunit.Assert.Throws<T>(paramName, action);

    /// <summary>
    /// Verifies that an action throws the expected argument exception parameter.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <param name="paramName">The expected parameter name.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The thrown exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldThrow<T>(this Action action, string? paramName, string userMessage)
        where T : ArgumentException =>
        WithUserMessage(userMessage, () => action.ShouldThrow<T>(paramName));

    /// <summary>
    /// Verifies that a value-returning action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The thrown exception.</returns>
    [SuppressMessage("Testing", "SKTEST017:Public assertions should have a userMessage overload", Justification = "ShouldThrow<T>(Func<object?>, string? paramName) already uses the (Func<object?>, string) signature, so a userMessage overload would not compile (CS0111); argument exceptions take a message through ShouldThrow<T>(paramName, userMessage).")]
    public static T ShouldThrow<T>(this Func<object?> action)
        where T : Exception => Xunit.Assert.Throws<T>(action);

    /// <summary>
    /// Verifies that a value-returning action throws the expected argument exception parameter.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <param name="paramName">The expected parameter name.</param>
    /// <returns>The thrown exception.</returns>
    public static T ShouldThrow<T>(this Func<object?> action, string? paramName)
        where T : ArgumentException => Xunit.Assert.Throws<T>(paramName, action);

    /// <summary>
    /// Verifies that a value-returning action throws the expected argument exception parameter.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <param name="paramName">The expected parameter name.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The thrown exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldThrow<T>(this Func<object?> action, string? paramName, string userMessage)
        where T : ArgumentException =>
        WithUserMessage(userMessage, () => action.ShouldThrow<T>(paramName));

    /// <summary>
    /// Verifies that an async action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <returns>The thrown exception.</returns>
    [SuppressMessage("Testing", "SKTEST017:Public assertions should have a userMessage overload", Justification = "ShouldThrow<T>(Func<Task>, string? paramName) already uses the (Func<Task>, string) signature, so a userMessage overload would not compile (CS0111); argument exceptions take a message through ShouldThrow<T>(paramName, userMessage).")]
    public static Task<T> ShouldThrow<T>(this Func<Task> action)
        where T : Exception => Xunit.Assert.ThrowsAsync<T>(action);

    /// <summary>
    /// Verifies that an async action throws the expected argument exception parameter.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <param name="paramName">The expected parameter name.</param>
    /// <returns>The thrown exception.</returns>
    public static Task<T> ShouldThrow<T>(this Func<Task> action, string? paramName)
        where T : ArgumentException => Xunit.Assert.ThrowsAsync<T>(paramName, action);

    /// <summary>
    /// Verifies that an async action throws the expected argument exception parameter.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <param name="paramName">The expected parameter name.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The thrown exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static Task<T> ShouldThrow<T>(this Func<Task> action, string? paramName, string userMessage)
        where T : ArgumentException =>
        WithUserMessage(userMessage, () => action.ShouldThrow<T>(paramName));

    /// <summary>
    /// Verifies that an async action throws an exception assignable to the expected type.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <returns>The thrown exception.</returns>
    public static Task<T> ShouldThrowAssignableTo<T>(this Func<Task> action)
        where T : Exception => Xunit.Assert.ThrowsAnyAsync<T>(action);

    /// <summary>
    /// Verifies that an async action throws an exception assignable to the expected type.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The thrown exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static Task<T> ShouldThrowAssignableTo<T>(this Func<Task> action, string userMessage)
        where T : Exception =>
        WithUserMessage(userMessage, () => action.ShouldThrowAssignableTo<T>());

    /// <summary>
    /// Verifies that a reflection invocation throws an inner exception of the expected type.
    /// </summary>
    /// <typeparam name="T">The expected inner exception type.</typeparam>
    /// <param name="action">The reflection action.</param>
    /// <returns>The typed inner exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public static T ShouldThrowInner<T>(this Action action)
        where T : Exception => ExceptionAssertions.ThrowsInner<T>(action);

    /// <summary>
    /// Verifies that a reflection invocation throws an inner exception of the expected type.
    /// </summary>
    /// <typeparam name="T">The expected inner exception type.</typeparam>
    /// <param name="action">The reflection action.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The typed inner exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldThrowInner<T>(this Action action, string userMessage)
        where T : Exception =>
        WithUserMessage(userMessage, () => action.ShouldThrowInner<T>());
}
