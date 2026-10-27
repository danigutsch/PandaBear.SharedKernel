using System.Reflection;

namespace PandaBear.Results.Tests;

/// <summary>
/// Reflection helper that builds malformed invalid results for the validation error tests.
/// </summary>
internal static class ValidationErrorsTestsHelpers
{
    /// <summary>
    /// Invokes the non-public Result constructor to build an Invalid result with the given error, which may be null or lack validation details.
    /// </summary>
    /// <param name="error">The error to store on the Invalid result, or null.</param>
    public static Result CreateMalformedInvalidResult(ResultError? error)
    {
        var constructor = typeof(Result).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(ResultError)],
            modifiers: null);

        _ = constructor.ShouldNotBeNull();
        return (Result)constructor.Invoke([ResultStatus.Invalid, error]);
    }
}
