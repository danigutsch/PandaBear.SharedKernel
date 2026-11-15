namespace PandaBear.Testing.Tests;

/// <summary>Generated hooks of <see cref="ICallProbe"/> as another assembly would have them, to provoke a mix-up.</summary>
internal sealed class ForeignCallProbeHooks : GeneratedCallHooks<ICallProbe>
{
    /// <inheritdoc />
    protected override ICallProbe Wrap(ICallProbe inner) => inner;
}
