using Xunit.Sdk;

namespace PandaBear.Testing.Assertions;

/// <summary>
/// Runs an assertion that takes no user message and puts the message before the failure that it reports.
/// </summary>
/// <remarks>
/// Argument errors, cancellation, and dynamic skips pass through unchanged. Every other exception becomes an
/// <see cref="XunitException"/> whose message starts with the user message and whose inner exception is the failure.
/// </remarks>
internal static class UserMessageAssertions
{
    /// <summary>Runs the assertion and prefixes the user message to any assertion failure.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="XunitException">Thrown when the assertion fails; the message starts with <paramref name="userMessage"/> followed by the original failure message.</exception>
    public static void WithUserMessage(string userMessage, Action assertion)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        try
        {
            assertion();
        }
        catch (Exception exception) when (IsFailure(exception))
        {
            throw Prepend(userMessage, exception);
        }
    }

    /// <summary>Runs the assertion, prefixes the user message to any assertion failure, and returns the result.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="XunitException">Thrown when the assertion fails; the message starts with <paramref name="userMessage"/> followed by the original failure message.</exception>
    public static T WithUserMessage<T>(string userMessage, Func<T> assertion)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        try
        {
            return assertion();
        }
        catch (Exception exception) when (IsFailure(exception))
        {
            throw Prepend(userMessage, exception);
        }
    }

    /// <summary>Runs the asynchronous assertion and prefixes the user message to any assertion failure.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="XunitException">Thrown when the assertion fails; the message starts with <paramref name="userMessage"/> followed by the original failure message.</exception>
    public static async Task WithUserMessage(string userMessage, Func<Task> assertion)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        try
        {
            await assertion().ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFailure(exception))
        {
            throw Prepend(userMessage, exception);
        }
    }

    /// <summary>Runs the asynchronous assertion, prefixes the user message to any assertion failure, and returns the result.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userMessage"/> is <see langword="null"/>.</exception>
    /// <exception cref="XunitException">Thrown when the assertion fails; the message starts with <paramref name="userMessage"/> followed by the original failure message.</exception>
    public static async Task<T> WithUserMessage<T>(string userMessage, Func<Task<T>> assertion)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        try
        {
            return await assertion().ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFailure(exception))
        {
            throw Prepend(userMessage, exception);
        }
    }

    // A dynamic skip is recognized by its message prefix, so prepending to it would turn the skip into a failure.
    private static bool IsFailure(Exception exception) =>
        exception is not (ArgumentException or OperationCanceledException or SkipException);

    private static XunitException Prepend(string userMessage, Exception failure) =>
        new(userMessage + Environment.NewLine + failure.Message, failure);
}
