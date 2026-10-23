namespace PandaBear.Testing.Tests;

internal sealed class UnformattableResultPayload
{
    public override string ToString() => throw new InvalidOperationException("The assertion must not format the payload.");
}
