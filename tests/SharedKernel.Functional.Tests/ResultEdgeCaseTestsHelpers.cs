using System.Reflection;

namespace SharedKernel.Functional.Tests;

internal static class ResultEdgeCaseTestsHelpers
{
    public static Result<string> CreateMalformedGenericResult(ResultStatus status, string? value, ResultError? error)
    {
        var constructor = typeof(Result<string>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(string), typeof(ResultError)],
            modifiers: null);

        Assert.NotNull(constructor);
        return (Result<string>)constructor.Invoke([status, value, error]);
    }

    public static Result CreateMalformedNonGenericResult(ResultStatus status, ResultError? error)
    {
        var constructor = typeof(Result).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(ResultError)],
            modifiers: null);

        Assert.NotNull(constructor);
        return (Result)constructor.Invoke([status, error]);
    }
}
