namespace PandaBear.Blazor.Tests;

/// <summary>Counts handled-failure increments.</summary>
internal sealed class RecordingCounter : IHandledFailureCounter
{
    /// <summary>Gets the number of increments.</summary>
    public int Count
    {
        get; private set;
    }

    /// <inheritdoc />
    public void IncrementHandled() => Count++;
}
