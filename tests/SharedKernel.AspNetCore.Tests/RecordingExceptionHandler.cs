using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Records the exceptions the exception-handler middleware passes on, without handling them.</summary>
internal sealed class RecordingExceptionHandler : IExceptionHandler
{
    /// <summary>Gets the observed exceptions in order.</summary>
    public List<Exception> Exceptions { get; } = [];

    /// <inheritdoc />
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Exceptions.Add(exception);
        return ValueTask.FromResult(false);
    }
}
