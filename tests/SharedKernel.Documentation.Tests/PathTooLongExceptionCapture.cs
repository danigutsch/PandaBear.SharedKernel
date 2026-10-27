using System.Runtime.ExceptionServices;

namespace PandaBear.Documentation.Tests;

/// <summary>Records the first and any later <see cref="IOException" /> thrown on the creating thread, even when the code under test catches them.</summary>
internal sealed class PathTooLongExceptionCapture : IDisposable
{
    private readonly int threadId = Environment.CurrentManagedThreadId;

    /// <summary>Starts listening for first-chance exceptions on the current thread.</summary>
    public PathTooLongExceptionCapture()
    {
        AppDomain.CurrentDomain.FirstChanceException += Capture;
    }

    /// <summary>Gets the first <see cref="IOException" /> observed on the creating thread, or <see langword="null" /> when none occurred.</summary>
    public IOException? FirstException
    {
        get; private set;
    }

    /// <summary>Gets the first <see cref="IOException" /> observed after <see cref="FirstException" /> that is a different instance, which signals a failure during cleanup.</summary>
    public IOException? CleanupException
    {
        get; private set;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        AppDomain.CurrentDomain.FirstChanceException -= Capture;
    }

    private void Capture(object? sender, FirstChanceExceptionEventArgs eventArgs)
    {
        if (Environment.CurrentManagedThreadId == threadId
            && eventArgs.Exception is IOException exception)
        {
            if (FirstException is null)
            {
                FirstException = exception;
            }
            else if (!ReferenceEquals(FirstException, exception))
            {
                CleanupException ??= exception;
            }
        }
    }
}
