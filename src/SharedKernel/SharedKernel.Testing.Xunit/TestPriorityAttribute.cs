using Xunit.v3;

namespace PandaBear.Testing;

/// <summary>
/// Declares the priority of the test. Emits the <c>Priority</c> trait, like <c>[Trait("Priority", ...)]</c>.
/// </summary>
/// <param name="priority">Declares the priority of the test.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestPriorityAttribute(TestPriority priority) : Attribute, ITraitAttribute
{
    private readonly string value = priority.ToTraitValue();

    /// <summary>
    /// Gets the declared value.
    /// </summary>
    public TestPriority Priority { get; } = priority;

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TestTraitNames.PriorityName, value)];
}
