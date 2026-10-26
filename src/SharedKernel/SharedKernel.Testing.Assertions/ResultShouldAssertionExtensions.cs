using PandaBear.Results;
using static PandaBear.Testing.Assertions.UserMessageAssertions;

namespace PandaBear.Testing.Assertions;

/// <summary>Provides assertions for operation results and their errors.</summary>
public static class ResultShouldAssertionExtensions
{
    /// <summary>Verifies success, including absence of error details.</summary>
    /// <param name="actual">The operation result.</param>
    public static void ShouldBeSuccess(this Result actual)
    {
        actual.IsSuccess.ShouldBeTrue(actual.IsSuccess ? null : $"Expected success but received status {actual.Status}.");
        actual.IsFailure.ShouldBeFalse();
        actual.ErrorDetails.ShouldBeNull();
    }

    /// <summary>Verifies success, including absence of error details.</summary>
    /// <param name="actual">The operation result.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldBeSuccess(this Result actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeSuccess());

    /// <summary>Verifies success and returns the successful value.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="actual">The operation result.</param>
    /// <returns>The successful value.</returns>
    public static T ShouldBeSuccess<T>(this Result<T> actual) where T : notnull
    {
        actual.IsSuccess.ShouldBeTrue(actual.IsSuccess ? null : $"Expected success but received status {actual.Status}.");
        actual.IsFailure.ShouldBeFalse();
        actual.ErrorDetails.ShouldBeNull();
        return actual.Value;
    }

    /// <summary>Verifies success and returns the successful value.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="actual">The operation result.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The successful value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static T ShouldBeSuccess<T>(this Result<T> actual, string userMessage) where T : notnull =>
        WithUserMessage(userMessage, () => actual.ShouldBeSuccess());

    /// <summary>Verifies a failure status and stable error code.</summary>
    /// <param name="actual">The operation result.</param>
    /// <param name="expectedStatus">The expected failure status.</param>
    /// <param name="expectedCode">The expected machine-readable error code.</param>
    /// <returns>The error for further assertions.</returns>
    public static ResultError ShouldBeFailure(this Result actual, ResultStatus expectedStatus, string expectedCode)
    {
        actual.IsFailure.ShouldBeTrue(actual.IsFailure ? null : $"Expected failure but received status {actual.Status}.");
        actual.IsSuccess.ShouldBeFalse();
        actual.Status.ShouldBe(expectedStatus);
        var error = actual.ErrorDetails.ShouldNotBeNull();
        error.Code.ShouldBe(expectedCode);
        return error;
    }

    /// <summary>Verifies a failure status and stable error code.</summary>
    /// <param name="actual">The operation result.</param>
    /// <param name="expectedStatus">The expected failure status.</param>
    /// <param name="expectedCode">The expected machine-readable error code.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The error for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static ResultError ShouldBeFailure(this Result actual, ResultStatus expectedStatus, string expectedCode, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeFailure(expectedStatus, expectedCode));

    /// <summary>Verifies a failure status and stable error code.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="actual">The operation result.</param>
    /// <param name="expectedStatus">The expected failure status.</param>
    /// <param name="expectedCode">The expected machine-readable error code.</param>
    /// <returns>The error for further assertions.</returns>
    public static ResultError ShouldBeFailure<T>(this Result<T> actual, ResultStatus expectedStatus, string expectedCode) where T : notnull
    {
        actual.IsFailure.ShouldBeTrue(actual.IsFailure ? null : $"Expected failure but received status {actual.Status}.");
        actual.TryGetValue(out _).ShouldBeFalse();
        return actual.ToResult().ShouldBeFailure(expectedStatus, expectedCode);
    }

    /// <summary>Verifies a failure status and stable error code.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="actual">The operation result.</param>
    /// <param name="expectedStatus">The expected failure status.</param>
    /// <param name="expectedCode">The expected machine-readable error code.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The error for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static ResultError ShouldBeFailure<T>(this Result<T> actual, ResultStatus expectedStatus, string expectedCode, string userMessage) where T : notnull =>
        WithUserMessage(userMessage, () => actual.ShouldBeFailure(expectedStatus, expectedCode));

    /// <summary>Verifies the exact ordered messages for one validation field, allowing other fields.</summary>
    /// <param name="actual">The error details; the assertion fails when they are null.</param>
    /// <param name="field">The case-sensitive field name.</param>
    /// <param name="expectedMessages">The expected messages in order.</param>
    public static void ShouldHaveValidationError(this ResultError? actual, string field, params string[] expectedMessages)
    {
        var error = actual.ShouldNotBeNull();
        var errors = error.ValidationErrors.ShouldNotBeNull();
        errors.TryGetValue(field, out var messages).ShouldBeTrue($"Expected validation field '{field}'.");
        messages.ShouldNotBeNull().ShouldBe(expectedMessages);
    }

    /// <summary>Verifies the exact ordered messages for one validation field, allowing other fields.</summary>
    /// <param name="actual">The error details; the assertion fails when they are null.</param>
    /// <param name="field">The case-sensitive field name.</param>
    /// <param name="expectedMessages">The expected messages in order.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is null.</exception>
    public static void ShouldHaveValidationError(this ResultError? actual, string field, string[] expectedMessages, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveValidationError(field, expectedMessages));
}
