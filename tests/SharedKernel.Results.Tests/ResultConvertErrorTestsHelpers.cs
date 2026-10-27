using System.Reflection;

namespace PandaBear.Results.Tests;

/// <summary>
/// Reflection helpers that build malformed results for the error conversion tests.
/// </summary>
internal static class ResultConvertErrorTestsHelpers
{
    /// <summary>
    /// Invokes the non-public generic Result constructor to build a result with an arbitrary status, value, and error.
    /// </summary>
    /// <param name="status">The status to assign, bypassing the factory methods.</param>
    /// <param name="value">The value to store in the result.</param>
    /// <param name="error">The error to store, or null.</param>
    public static Result<T> CreateMalformedGenericResult<T>(ResultStatus status, T value, ResultError? error)
        where T : notnull
    {
        var constructor = typeof(Result<T>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(T), typeof(ResultError)],
            modifiers: null);

        _ = constructor.ShouldNotBeNull();
        return (Result<T>)constructor.Invoke([status, value, error]);
    }

    /// <summary>
    /// Invokes the non-public Result constructor to build a result with an arbitrary status and error.
    /// </summary>
    /// <param name="status">The status to assign, bypassing the factory methods.</param>
    /// <param name="error">The error to store, or null.</param>
    public static Result CreateMalformedResult(ResultStatus status, ResultError? error)
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
