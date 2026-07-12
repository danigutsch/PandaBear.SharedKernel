using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
    public static void ShouldBe<T>(this T actual, T expected) => Xunit.Assert.Equal(expected, actual);

    /// <summary>
    /// Verifies equality with a nullable expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public static void ShouldBe<T>(this T actual, T? expected)
        where T : struct => Xunit.Assert.Equal(expected, actual);

    /// <summary>
    /// Verifies equality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldBe<T>(this T actual, T expected, IEqualityComparer<T> comparer) => Xunit.Assert.Equal(expected, actual, comparer);

    /// <summary>
    /// Verifies floating-point equality with precision.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="precision">The precision.</param>
    public static void ShouldBe(this double actual, double expected, int precision) => Xunit.Assert.Equal(expected, actual, precision);

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
    /// Verifies inequality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    public static void ShouldNotBe<T>(this T actual, T expected) => Xunit.Assert.NotEqual(expected, actual);

    /// <summary>
    /// Verifies inequality with the expected value using a comparer.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldNotBe<T>(this T actual, T expected, IEqualityComparer<T> comparer) => Xunit.Assert.NotEqual(expected, actual, comparer);

    /// <summary>
    /// Verifies reference equality with the expected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected reference.</param>
    public static void ShouldBeSameAs<T>(this T? actual, T? expected)
        where T : class => Xunit.Assert.Same(expected, actual);

    /// <summary>
    /// Verifies reference inequality with the unexpected value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="unexpected">The unexpected reference.</param>
    public static void ShouldNotBeSameAs<T>(this T? actual, T? unexpected)
        where T : class => Xunit.Assert.NotSame(unexpected, actual);

    /// <summary>
    /// Verifies type equality.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeOfType<T>(this object? actual) => Xunit.Assert.IsType<T>(actual);

    /// <summary>
    /// Verifies type equality, optionally allowing derived types.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="exactMatch">Whether the type must match exactly.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeOfType<T>(this object? actual, bool exactMatch) => Xunit.Assert.IsType<T>(actual, exactMatch);

    /// <summary>
    /// Verifies type inequality.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    public static void ShouldNotBeOfType<T>(this object? actual) => Xunit.Assert.IsNotType<T>(actual);

    /// <summary>
    /// Verifies assignability.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <returns>The typed value.</returns>
    public static T ShouldBeAssignableTo<T>(this object? actual) => Xunit.Assert.IsAssignableFrom<T>(actual);

    /// <summary>
    /// Verifies non-assignability.
    /// </summary>
    /// <typeparam name="T">The unexpected type.</typeparam>
    /// <param name="actual">The actual value.</param>
    public static void ShouldNotBeAssignableTo<T>(this object? actual) => Xunit.Assert.IsNotAssignableFrom<T>(actual);

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
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    public static void ShouldBeNull(this object? actual) => Xunit.Assert.Null(actual);

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
    /// Verifies that a string contains the expected fragment.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragment.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldContain(this string? actual, string expected, StringComparison comparisonType) => Xunit.Assert.Contains(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a collection contains the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected) => Xunit.Assert.Contains(expected, actual);

    /// <summary>
    /// Verifies that a collection contains the expected item using a comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    /// <param name="comparer">The equality comparer.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected, IEqualityComparer<T> comparer) => Xunit.Assert.Contains(expected, actual, comparer);

    /// <summary>
    /// Verifies that a collection contains a matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    public static void ShouldContain<T>(this IEnumerable<T> actual, Predicate<T> predicate) => Xunit.Assert.Contains(actual, predicate);

    /// <summary>
    /// Verifies that a string contains every expected fragment in ordinal order.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected fragments.</param>
    public static void ShouldContainInOrder(this string? actual, params string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var startIndex = 0;
        foreach (var fragment in expected)
        {
            ArgumentNullException.ThrowIfNull(fragment);

            var index = actual?.IndexOf(fragment, startIndex, StringComparison.Ordinal) ?? -1;
            Xunit.Assert.True(index >= 0, $"Expected string to contain '{fragment}' after the prior fragment.");
            startIndex = index + fragment.Length;
        }
    }

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
    /// Verifies that a string ends with the expected suffix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldEndWith(this string? actual, string expected, StringComparison comparisonType) => Xunit.Assert.EndsWith(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a string ends with the expected suffix using ordinal comparison.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected suffix.</param>
    public static void ShouldEndWith(this string? actual, string expected) => Xunit.Assert.EndsWith(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a collection does not contain the expected item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="expected">The expected item.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, T expected) => Xunit.Assert.DoesNotContain(expected, actual);

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
    /// Verifies that a collection does not contain a matching item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="predicate">The item predicate.</param>
    public static void ShouldNotContain<T>(this IEnumerable<T> actual, Predicate<T> predicate) => Xunit.Assert.DoesNotContain(actual, predicate);

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
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldNotContain(this string? actual, string expected, StringComparison comparisonType) => Xunit.Assert.DoesNotContain(expected, actual, comparisonType);

    /// <summary>
    /// Verifies that a collection is empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    public static void ShouldBeEmpty<T>(this IEnumerable<T> actual) => Xunit.Assert.Empty(actual);

    /// <summary>
    /// Verifies that a collection is not empty.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    public static void ShouldNotBeEmpty<T>(this IEnumerable<T> actual) => Xunit.Assert.NotEmpty(actual);

    /// <summary>
    /// Verifies that a collection contains exactly one item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <returns>The single item.</returns>
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> actual) => Xunit.Assert.Single(actual);

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
    /// Verifies that a value is within a range.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    public static void ShouldBeInRange<T>(this T actual, T low, T high)
        where T : IComparable => Xunit.Assert.InRange(actual, low, high);

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
    public static void ShouldStartWith(this string? actual, string expected) => Xunit.Assert.StartsWith(expected, actual, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string starts with the expected prefix.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected prefix.</param>
    /// <param name="comparisonType">The string comparison type.</param>
    public static void ShouldStartWith(this string? actual, string expected, StringComparison comparisonType) =>
        Xunit.Assert.StartsWith(expected, actual, comparisonType);

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
    /// <param name="expectedRegex">The expected regular expression.</param>
    public static void ShouldMatch(this string? actual, Regex expectedRegex) => Xunit.Assert.Matches(expectedRegex, actual);

    /// <summary>
    /// Verifies that a string does not match a regular expression.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expectedRegex">The unexpected regular expression.</param>
    public static void ShouldNotMatch(this string? actual, Regex expectedRegex) => Xunit.Assert.DoesNotMatch(expectedRegex, actual);

    /// <summary>
    /// Verifies every item in a collection.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="action">The inspector.</param>
    public static void ShouldAllSatisfy<T>(this IEnumerable<T> actual, Action<T> action) => Xunit.Assert.All(actual, action);

    /// <summary>
    /// Verifies collection items with ordered inspectors.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="actual">The actual collection.</param>
    /// <param name="inspectors">The ordered item inspectors.</param>
    public static void ShouldMatchCollection<T>(this IEnumerable<T> actual, params Action<T>[] inspectors) =>
        Xunit.Assert.Collection(actual, inspectors);

    /// <summary>
    /// Verifies that an action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The thrown exception.</returns>
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
    /// Verifies that a value-returning action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The thrown exception.</returns>
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
    /// Verifies that an async action throws the expected exception.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <returns>The thrown exception.</returns>
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
    /// Verifies that an async action throws an exception assignable to the expected type.
    /// </summary>
    /// <typeparam name="T">The expected exception type.</typeparam>
    /// <param name="action">The async action.</param>
    /// <returns>The thrown exception.</returns>
    public static Task<T> ShouldThrowAssignableTo<T>(this Func<Task> action)
        where T : Exception => Xunit.Assert.ThrowsAnyAsync<T>(action);

    /// <summary>
    /// Verifies that a reflection invocation throws an inner exception of the expected type.
    /// </summary>
    /// <typeparam name="T">The expected inner exception type.</typeparam>
    /// <param name="action">The reflection action.</param>
    /// <returns>The typed inner exception.</returns>
    public static T ShouldThrowInner<T>(this Action action)
        where T : Exception => ExceptionAssertions.ThrowsInner<T>(action);
}
