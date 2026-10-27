namespace PandaBear.Testing.Tests;

/// <summary>Result payload whose <see cref="ToString" /> throws, proving an assertion never formats the value it checks.</summary>
internal sealed class UnformattableResultPayload
{
    /// <summary>Always throws <see cref="InvalidOperationException" /> so any attempt to format the payload fails the test.</summary>
    /// <returns>Never returns.</returns>
    public override string ToString() => throw new InvalidOperationException("The assertion must not format the payload.");
}
