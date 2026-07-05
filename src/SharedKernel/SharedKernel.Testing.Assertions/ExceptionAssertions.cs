using System.Reflection;

namespace SharedKernel.Testing.Assertions;

/// <summary>
/// Shared assertions for exception patterns used by reflection-heavy tests.
/// </summary>
public static class ExceptionAssertions
{
    /// <summary>
    /// Verifies that a reflection invocation threw an inner exception of the expected type.
    /// </summary>
    /// <typeparam name="TException">The expected inner exception type.</typeparam>
    /// <param name="action">The reflection invocation.</param>
    /// <returns>The typed inner exception.</returns>
    public static TException ThrowsInner<TException>(Action action)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is TException typedException)
        {
            return typedException;
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException(
                $"Expected inner exception '{typeof(TException).FullName}', but got '{exception.InnerException?.GetType().FullName ?? "null"}'.",
                exception);
        }

        throw new InvalidOperationException($"Expected '{typeof(TargetInvocationException).FullName}' to be thrown.");
    }
}
