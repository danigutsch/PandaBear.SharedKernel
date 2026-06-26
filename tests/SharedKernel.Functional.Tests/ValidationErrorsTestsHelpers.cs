using System.Reflection;

namespace SharedKernel.Functional.Tests;

internal static class ValidationErrorsTestsHelpers
{
    public static Result CreateMalformedInvalidResult(ResultError? error)
    {
        var constructor = typeof(Result).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(ResultError)],
            modifiers: null);

        Assert.NotNull(constructor);
        return (Result)constructor.Invoke([ResultStatus.Invalid, error]);
    }
}
