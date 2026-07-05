using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SharedKernel.Testing.Assertions;

/// <summary>
/// Provides repository-owned test assertions while preserving xUnit assertion behavior.
/// </summary>
public static class TestAssert
{
    /// <summary>
    /// Fails the current test with the provided message.
    /// </summary>
    /// <param name="message">The failure message.</param>
    [DoesNotReturn]
    public static void Fail(string? message = null) => Xunit.Assert.Fail(message ?? "Assertion failed.");

    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    public static void True(bool condition, string? userMessage = null) => Xunit.Assert.True(condition, userMessage);

    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    public static void True(bool? condition, string? userMessage = null) => Xunit.Assert.True(condition, userMessage);

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    public static void False(bool condition, string? userMessage = null) => Xunit.Assert.False(condition, userMessage);

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    public static void False(bool? condition, string? userMessage = null) => Xunit.Assert.False(condition, userMessage);

    /// <summary>
    /// Verifies equality.
    /// </summary>
    public static void Equal<T>(T expected, T actual) => Xunit.Assert.Equal(expected, actual);

    /// <summary>
    /// Verifies equality using a comparer.
    /// </summary>
    public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer) => Xunit.Assert.Equal(expected, actual, comparer);

    /// <summary>
    /// Verifies floating-point equality with precision.
    /// </summary>
    public static void Equal(double expected, double actual, int precision) => Xunit.Assert.Equal(expected, actual, precision);

    /// <summary>
    /// Verifies floating-point equality with precision.
    /// </summary>
    public static void Equal(double expected, double actual, int precision, MidpointRounding rounding) => Xunit.Assert.Equal(expected, actual, precision, rounding);

    /// <summary>
    /// Verifies inequality.
    /// </summary>
    public static void NotEqual<T>(T expected, T actual) => Xunit.Assert.NotEqual(expected, actual);

    /// <summary>
    /// Verifies inequality using a comparer.
    /// </summary>
    public static void NotEqual<T>(T expected, T actual, IEqualityComparer<T> comparer) => Xunit.Assert.NotEqual(expected, actual, comparer);

    /// <summary>
    /// Verifies that a value is null.
    /// </summary>
    public static void Null(object? value) => Xunit.Assert.Null(value);

    /// <summary>
    /// Verifies that a nullable reference is not null and updates compiler null-state flow.
    /// </summary>
    public static T NotNull<T>([NotNull] T? value)
        where T : class
    {
        Xunit.Assert.NotNull(value);
        return value;
    }

    /// <summary>
    /// Verifies that a nullable value type is not null.
    /// </summary>
    public static T NotNull<T>([NotNull] T? value)
        where T : struct => Xunit.Assert.NotNull(value);

    /// <summary>
    /// Verifies reference equality.
    /// </summary>
    public static void Same(object? expected, object? actual) => Xunit.Assert.Same(expected, actual);

    /// <summary>
    /// Verifies that a collection is empty.
    /// </summary>
    public static void Empty<T>(IEnumerable<T> collection) => Xunit.Assert.Empty(collection);

    /// <summary>
    /// Verifies that a collection is not empty.
    /// </summary>
    public static void NotEmpty<T>(IEnumerable<T> collection) => Xunit.Assert.NotEmpty(collection);

    /// <summary>
    /// Verifies that a collection contains exactly one item.
    /// </summary>
    public static T ExactlyOne<T>(IEnumerable<T> collection) => Xunit.Assert.Single(collection);

    /// <summary>
    /// Verifies that a collection contains an expected item.
    /// </summary>
    public static void Contains<T>(T expected, IEnumerable<T> collection) => Xunit.Assert.Contains(expected, collection);

    /// <summary>
    /// Verifies that a collection contains an expected item using a comparer.
    /// </summary>
    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer) => Xunit.Assert.Contains(expected, collection, comparer);

    /// <summary>
    /// Verifies that a collection contains a matching item.
    /// </summary>
    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> predicate) => Xunit.Assert.Contains(collection, predicate);

    /// <summary>
    /// Verifies that a string contains an expected fragment.
    /// </summary>
    public static void Contains(string expectedSubstring, string? actualString) => Xunit.Assert.Contains(expectedSubstring, actualString, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string contains an expected fragment.
    /// </summary>
    public static void Contains(string expectedSubstring, string? actualString, StringComparison comparisonType) => Xunit.Assert.Contains(expectedSubstring, actualString, comparisonType);

    /// <summary>
    /// Verifies that a collection does not contain an expected item.
    /// </summary>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection) => Xunit.Assert.DoesNotContain(expected, collection);

    /// <summary>
    /// Verifies that a collection does not contain a matching item.
    /// </summary>
    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> predicate) => Xunit.Assert.DoesNotContain(collection, predicate);

    /// <summary>
    /// Verifies that a string does not contain an expected fragment.
    /// </summary>
    public static void DoesNotContain(string expectedSubstring, string? actualString) => Xunit.Assert.DoesNotContain(expectedSubstring, actualString, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string does not contain an expected fragment.
    /// </summary>
    public static void DoesNotContain(string expectedSubstring, string? actualString, StringComparison comparisonType) => Xunit.Assert.DoesNotContain(expectedSubstring, actualString, comparisonType);

    /// <summary>
    /// Verifies every item in a collection.
    /// </summary>
    public static void All<T>(IEnumerable<T> collection, Action<T> action) => Xunit.Assert.All(collection, action);

    /// <summary>
    /// Verifies items in a collection with inspectors.
    /// </summary>
    public static void Collection<T>(IEnumerable<T> collection, params Action<T>[] inspectors) => Xunit.Assert.Collection(collection, inspectors);

    /// <summary>
    /// Verifies type equality.
    /// </summary>
    public static T IsType<T>(object? value) => Xunit.Assert.IsType<T>(value);

    /// <summary>
    /// Verifies type inequality.
    /// </summary>
    public static void IsNotType<T>(object? value) => Xunit.Assert.IsNotType<T>(value);

    /// <summary>
    /// Verifies assignability.
    /// </summary>
    public static T IsAssignableFrom<T>(object? value) => Xunit.Assert.IsAssignableFrom<T>(value);

    /// <summary>
    /// Verifies non-assignability.
    /// </summary>
    public static void IsNotAssignableFrom<T>(object? value) => Xunit.Assert.IsNotAssignableFrom<T>(value);

    /// <summary>
    /// Verifies that a value is within a range.
    /// </summary>
    public static void InRange<T>(T actual, T low, T high)
        where T : IComparable => Xunit.Assert.InRange(actual, low, high);

    /// <summary>
    /// Verifies that a string starts with the expected value.
    /// </summary>
    public static void StartsWith(string expectedStartString, string? actualString) => Xunit.Assert.StartsWith(expectedStartString, actualString, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string starts with the expected value.
    /// </summary>
    public static void StartsWith(string expectedStartString, string? actualString, StringComparison comparisonType) => Xunit.Assert.StartsWith(expectedStartString, actualString, comparisonType);

    /// <summary>
    /// Verifies that a string ends with the expected value.
    /// </summary>
    public static void EndsWith(string expectedEndString, string? actualString) => Xunit.Assert.EndsWith(expectedEndString, actualString, StringComparison.Ordinal);

    /// <summary>
    /// Verifies that a string ends with the expected value.
    /// </summary>
    public static void EndsWith(string expectedEndString, string? actualString, StringComparison comparisonType) => Xunit.Assert.EndsWith(expectedEndString, actualString, comparisonType);

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    public static void Matches(string expectedRegexPattern, string? actualString) => Xunit.Assert.Matches(expectedRegexPattern, actualString);

    /// <summary>
    /// Verifies that a string matches a regular expression.
    /// </summary>
    public static void Matches(Regex expectedRegex, string? actualString) => Xunit.Assert.Matches(expectedRegex, actualString);

    /// <summary>
    /// Verifies that a string does not match a regular expression.
    /// </summary>
    public static void DoesNotMatch(Regex expectedRegex, string? actualString) => Xunit.Assert.DoesNotMatch(expectedRegex, actualString);

    /// <summary>
    /// Verifies that an action throws the expected exception.
    /// </summary>
    public static T Throws<T>(Action testCode)
        where T : Exception => Xunit.Assert.Throws<T>(testCode);

    /// <summary>
    /// Verifies that a value-returning action throws the expected exception.
    /// </summary>
    public static T Throws<T>(Func<object?> testCode)
        where T : Exception => Xunit.Assert.Throws<T>(testCode);

    /// <summary>
    /// Verifies that an action throws the expected exception.
    /// </summary>
    public static T Throws<T>(string? paramName, Action testCode)
        where T : ArgumentException => Xunit.Assert.Throws<T>(paramName, testCode);

    /// <summary>
    /// Verifies that a value-returning action throws the expected exception.
    /// </summary>
    public static T Throws<T>(string? paramName, Func<object?> testCode)
        where T : ArgumentException => Xunit.Assert.Throws<T>(paramName, testCode);

    /// <summary>
    /// Verifies that an async action throws the expected exception.
    /// </summary>
    public static Task<T> Throws<T>(Func<Task> testCode)
        where T : Exception => Xunit.Assert.ThrowsAsync<T>(testCode);

    /// <summary>
    /// Verifies that an async action throws the expected exception.
    /// </summary>
    public static Task<T> Throws<T>(string? paramName, Func<Task> testCode)
        where T : ArgumentException => Xunit.Assert.ThrowsAsync<T>(paramName, testCode);

    /// <summary>
    /// Verifies that an async action throws an exception assignable to the expected type.
    /// </summary>
    public static Task<T> ThrowsAny<T>(Func<Task> testCode)
        where T : Exception => Xunit.Assert.ThrowsAnyAsync<T>(testCode);
}
