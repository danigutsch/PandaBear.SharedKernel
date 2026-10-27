namespace PandaBear.Blazor.Tests;

/// <summary>A counter that always fails.</summary>
internal sealed class ThrowingCounter : IHandledFailureCounter
{
    /// <summary>The message that must never reach the log.</summary>
    public const string SecretMessage = "meter-secret";

    /// <inheritdoc />
    public void IncrementHandled() => throw new InvalidOperationException(SecretMessage);
}
