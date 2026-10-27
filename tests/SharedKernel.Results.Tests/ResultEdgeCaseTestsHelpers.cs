using System.Reflection;

namespace PandaBear.Results.Tests;

/// <summary>
/// Reflection helpers that build malformed results for the result edge case tests.
/// </summary>
internal static class ResultEdgeCaseTestsHelpers
{
    /// <summary>
    /// Invokes the non-public generic Result constructor to build a string result with an arbitrary status, value, and error.
    /// </summary>
    /// <param name="status">The status to assign, bypassing the factory methods.</param>
    /// <param name="value">The value to store, or null.</param>
    /// <param name="error">The error to store, or null.</param>
    public static Result<string> CreateMalformedGenericResult(ResultStatus status, string? value, ResultError? error)
    {
        var constructor = typeof(Result<string>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(string), typeof(ResultError)],
            modifiers: null);

        _ = constructor.ShouldNotBeNull();
        return (Result<string>)constructor.Invoke([status, value, error]);
    }

    /// <summary>
    /// Invokes the non-public Result constructor to build a result with an arbitrary status and error.
    /// </summary>
    /// <param name="status">The status to assign, bypassing the factory methods.</param>
    /// <param name="error">The error to store, or null.</param>
    public static Result CreateMalformedNonGenericResult(ResultStatus status, ResultError? error)
    {
        var constructor = typeof(Result).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(ResultError)],
            modifiers: null);

        _ = constructor.ShouldNotBeNull();
        return (Result)constructor.Invoke([status, error]);
    }
}
