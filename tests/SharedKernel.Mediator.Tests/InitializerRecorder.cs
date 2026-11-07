using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Records the two providers a scope initializer received, and the markers they hold.</summary>
internal sealed class InitializerRecorder
{
    /// <summary>Gets the first provider received (the caller scope).</summary>
    public IServiceProvider? Caller
    {
        get; private set;
    }

    /// <summary>Gets the second provider received (the operation scope).</summary>
    public IServiceProvider? Operation
    {
        get; private set;
    }

    /// <summary>Gets the marker resolved from the caller scope.</summary>
    public ScopeMarker? CallerMarker
    {
        get; private set;
    }

    /// <summary>Gets the marker resolved from the operation scope.</summary>
    public ScopeMarker? OperationMarker
    {
        get; private set;
    }

    /// <summary>Records the providers and their markers.</summary>
    /// <param name="caller">The caller scope provider.</param>
    /// <param name="operation">The operation scope provider.</param>
    /// <returns>A completed task.</returns>
    public ValueTask Record(IServiceProvider caller, IServiceProvider operation)
    {
        Caller = caller;
        Operation = operation;
        CallerMarker = caller.GetRequiredService<ScopeMarker>();
        OperationMarker = operation.GetRequiredService<ScopeMarker>();
        return ValueTask.CompletedTask;
    }
}
