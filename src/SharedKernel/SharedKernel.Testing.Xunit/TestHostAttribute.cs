using Xunit.v3;

namespace PandaBear.Testing;

/// <summary>
/// Declares how the test is hosted. Emits the <c>Host</c> trait, like <c>[Trait("Host", ...)]</c>.
/// </summary>
/// <param name="host">Declares how the test is hosted.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestHostAttribute(TestHost host) : Attribute, ITraitAttribute
{
    private readonly string value = host.ToTraitValue();

    /// <summary>
    /// Gets the declared value.
    /// </summary>
    public TestHost Host { get; } = host;

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TestTraitNames.HostName, value)];
}
