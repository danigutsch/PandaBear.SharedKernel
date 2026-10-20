using System.Runtime.ExceptionServices;

namespace SharedKernel.Documentation.Tests;

internal sealed class PathTooLongExceptionCapture : IDisposable
{
    private readonly int threadId = Environment.CurrentManagedThreadId;

    public PathTooLongExceptionCapture()
    {
        AppDomain.CurrentDomain.FirstChanceException += Capture;
    }

    public IOException? FirstException { get; private set; }

    public IOException? CleanupException { get; private set; }

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
