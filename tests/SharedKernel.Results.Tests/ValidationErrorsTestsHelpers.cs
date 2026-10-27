using System.Reflection;

namespace PandaBear.Results.Tests;

internal static class ValidationErrorsTestsHelpers
{
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
