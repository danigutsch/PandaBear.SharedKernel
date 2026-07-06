using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SharedKernel.Testing.Assertions;

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
    public static void ShouldBe<T>(this T actual, T expected) => TestAssert.Equal(expected, actual);

    /// <summary>
    /// Verifies equality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldBe<T>(this T actual, T expected, IEqualityComparer<T> comparer) => TestAssert.Equal(expected, actual, comparer);

    /// <summary>
    /// Verifies floating-point equality with precision.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="precision">The precision.</param>
    public static void ShouldBe(this double actual, double expected, int precision) => TestAssert.Equal(expected, actual, precision);

    /// <summary>
    /// Verifies inequality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public static void ShouldNotBe<T>(this T actual, T expected) => TestAssert.NotEqual(expected, actual);

    /// <summary>
    /// Verifies inequality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldNotBe<T>(this T actual, T expected, IEqualityComparer<T> comparer) => TestAssert.NotEqual(expected, actual, comparer);

    /// <summary>
    /// Verifies reference equality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected reference.</param>
    public static void ShouldBeSameAs<T>(this T? actual, T? expected)
        where T : class => TestAssert.Same(expected, actual);

    /// <summary>
    /// Verifies reference inequality with the unexpected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="unexpected">The unexpected reference.</param>
    public static void ShouldNotBeSameAs<T>(this T? actual, T? unexpected)
        where T : class => TestAssert.NotSame(unexpected, actual);

    /// <summary>
    /// Verifies type equality.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeOfType<T>(this object? actual) => TestAssert.IsType<T>(actual);

    /// <summary>
    /// Verifies assignability.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeAssignableTo<T>(this object? actual) => TestAssert.IsAssignableFrom<T>(actual);

    /// <summary>
    /// Verifies non-assignability.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    public static void ShouldNotBeAssignableTo<T>(this object? actual) => TestAssert.IsNotAssignableFrom<T>(actual);

    /// <summary>
    /// Verifies that a nullable reference is not null and updates compiler null-state flow.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The non-null value.</returns>
    public static T ShouldNotBeNull<T>([NotNull] this T? actual)
        where T : class => TestAssert.NotNull(actual);

    /// <summary>
    /// Verifies that a nullable value type is not null.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The non-null value.</returns>
    public static T ShouldNotBeNull<T>([NotNull] this T? actual)
        where T : struct => TestAssert.NotNull(actual);

    /// <summary>
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    public static void ShouldBeNull(this object? actual) => TestAssert.Null(actual);

    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeTrue(this bool actual, string? userMessage = null) => TestAssert.True(actual, userMessage);

    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeTrue(this bool? actual, string? userMessage = null) => TestAssert.True(actual, userMessage);

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeFalse(this bool actual, string? userMessage = null) => TestAssert.False(actual, userMessage);

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="userMessage">The failure message.</param>
    public static void ShouldBeFalse(this bool? actual, string? userMessage = null) => TestAssert.False(actual, userMessage);

    /// <summary>
    /// Verifies that a string contains the expected fragment with ordinal comparison.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    public static void ShouldContain(this string? actual, string expected) => TestAssert.Contains(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string contains the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldContain(this string? actual, string expected, StringComparison comparisonType) => TestAssert.Contains(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a collection contains the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected) => TestAssert.Contains(expected, actual);

    /// <summary>
    /// Verifies that a collection contains the expected item using a comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T> comparer) => TestAssert.Contains(expected, actual, comparer);

    /// <summary>
    /// Verifies that a collection contains a matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, Predicate<T> predicate) => TestAssert.Contains(actual, predicate);

    /// <summary>
    /// Verifies that a string ends with the expected suffix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldEndWith(this string? actual, string expected, StringComparison comparisonType) => TestAssert.EndsWith(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a collection does not contain the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, T expected) => TestAssert.DoesNotContain(expected, actual);

    /// <summary>
    /// Verifies that a collection does not contain a matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, Predicate<T> predicate) => TestAssert.DoesNotContain(actual, predicate);

    /// <summary>
    /// Verifies that a string does not contain the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    public static void ShouldNotContain(this string? actual, string expected) => TestAssert.DoesNotContain(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a collection is empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    public static void ShouldBeEmpty<T>(this IEnumerable<T> actual) => TestAssert.Empty(actual);

    /// <summary>
    /// Verifies that a collection is not empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    public static void ShouldNotBeEmpty<T>(this IEnumerable<T> actual) => TestAssert.NotEmpty(actual);

    /// <summary>
    /// Verifies that a collection contains exactly one item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <returns>The single item.</returns>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual) => TestAssert.ExactlyOne(actual);

    /// <summary>
    /// Verifies that a collection contains exactly one matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    /// <returns>The single matching item.</returns>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual, Predicate<T> predicate) =>
        TestAssert.ExactlyOne(actual.Where(item => predicate(item)));

    /// <summary>
    /// Verifies that a value is within a range.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    public static void ShouldBeInRange<T>(this T actual, T low, T high)
        where T : IComparable => TestAssert.InRange(actual, low, high);

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
    /// Verifies that a value is greater than or equal to the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The inclusive lower bound.</param>
    public static void ShouldBeGreaterThanOrEqualTo<T>(this T actual, T expected)
        where T : IComparable<T> => (actual.CompareTo(expected) >= 0).ShouldBeTrue(
            $"Expected value greater than or equal to {expected}, but found {actual}.");

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
    /// Verifies that a value is less than or equal to the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The inclusive upper bound.</param>
    public static void ShouldBeLessThanOrEqualTo<T>(this T actual, T expected)
        where T : IComparable<T> => (actual.CompareTo(expected) <= 0).ShouldBeTrue(
            $"Expected value less than or equal to {expected}, but found {actual}.");

    /// <summary>
    /// Verifies that a string starts with the expected value.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected start value.</param>
    public static void ShouldStartWith(this string? actual, string expected) => TestAssert.StartsWith(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegexPattern">The expected regular expression pattern.</param>
    public static void ShouldMatch(this string? actual, string expectedRegexPattern) => TestAssert.Matches(expectedRegexPattern, actual);

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegex">The expected regular expression.</param>
    public static void ShouldMatch(this string? actual, Regex expectedRegex) => TestAssert.Matches(expectedRegex, actual);

    /// <summary>
    /// Verifies every item in a collection.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="action">The inspector.</param>
    public static void ShouldAllSatisfy<T>(this IEnumerable<T> actual, Action<T> action) => TestAssert.All(actual, action);

    /// <summary>
    /// Verifies that an action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The thrown exception.</returns>
    public static T ShouldThrow<T>(this Action action)
        where T : Exception => TestAssert.Throws<T>(action);

    /// <summary>
    /// Verifies that an async action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <returns>The thrown exception.</returns>
    public static Task<T> ShouldThrow<T>(this Func<Task> action)
        where T : Exception => TestAssert.Throws<T>(action);

    /// <summary>
    /// Verifies that a reflection invocation throws an inner exception of the expected type.
    /// </summary>
    /// <typeparam name="T">The expected inner exception type.</typeparam>
    /// <param name="action">The reflection action.</param>
    /// <returns>The typed inner exception.</returns>
    public static T ShouldThrowInner<T>(this Action action)
        where T : Exception => ExceptionAssertions.ThrowsInner<T>(action);
}
