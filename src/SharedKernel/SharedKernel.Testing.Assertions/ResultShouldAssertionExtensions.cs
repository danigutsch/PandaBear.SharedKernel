using PandaBear.Results;

namespace PandaBear.Testing.Assertions;

/// <summary>Provides assertions for operation results and their errors.</summary>
public static class ResultShouldAssertionExtensions
{
    /// <summary>Verifies success, including absence of error details.</summary>
    /// <param name="actual">The operation result.</param>
    public static void ShouldBeSuccess(this Result actual)
    {
        actual.IsSuccess.ShouldBeTrue($"Expected success but received {actual}.");
        actual.IsFailure.ShouldBeFalse();
        actual.ErrorDetails.ShouldBeNull();
    }

    /// <summary>Verifies success and returns the successful value.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="actual">The operation result.</param>
    /// <returns>The successful value.</returns>
    public static T ShouldBeSuccess<T>(this Result<T> actual) where T : notnull
    {
        actual.IsSuccess.ShouldBeTrue($"Expected success but received {actual}.");
        actual.IsFailure.ShouldBeFalse();
        actual.ErrorDetails.ShouldBeNull();
        return actual.Value;
    }

    /// <summary>Verifies a failure status and stable error code.</summary>
    /// <param name="actual">The operation result.</param>
    /// <param name="expectedStatus">The expected failure status.</param>
    /// <param name="expectedCode">The expected machine-readable error code.</param>
    /// <returns>The error for further assertions.</returns>
    public static ResultError ShouldBeFailure(this Result actual, ResultStatus expectedStatus, string expectedCode)
    {
        actual.IsFailure.ShouldBeTrue($"Expected failure but received {actual}.");
        actual.IsSuccess.ShouldBeFalse();
        actual.Status.ShouldBe(expectedStatus);
        var error = actual.ErrorDetails.ShouldNotBeNull();
        error.Code.ShouldBe(expectedCode);
        return error;
    }

    /// <summary>Verifies a failure status and stable error code.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="actual">The operation result.</param>
    /// <param name="expectedStatus">The expected failure status.</param>
    /// <param name="expectedCode">The expected machine-readable error code.</param>
    /// <returns>The error for further assertions.</returns>
    public static ResultError ShouldBeFailure<T>(this Result<T> actual, ResultStatus expectedStatus, string expectedCode) where T : notnull
    {
        actual.IsFailure.ShouldBeTrue($"Expected failure but received {actual}.");
        actual.TryGetValue(out _).ShouldBeFalse();
        return actual.ToResult().ShouldBeFailure(expectedStatus, expectedCode);
    }

    /// <summary>Verifies the exact ordered messages for one validation field, allowing other fields.</summary>
    /// <param name="actual">The error details.</param>
    /// <param name="field">The case-sensitive field name.</param>
    /// <param name="expectedMessages">The expected messages in order.</param>
    public static void ShouldHaveValidationError(this ResultError actual, string field, params string[] expectedMessages)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var errors = actual.ValidationErrors.ShouldNotBeNull();
        errors.TryGetValue(field, out var messages).ShouldBeTrue($"Expected validation field '{field}'.");
        messages.ShouldNotBeNull().ShouldBe(expectedMessages);
    }
}
