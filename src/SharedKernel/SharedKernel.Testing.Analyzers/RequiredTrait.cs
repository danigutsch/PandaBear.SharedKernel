namespace SharedKernel.Testing.Analyzers;

/// <summary>
/// Represents trait metadata required for xUnit test methods by analyzer configuration.
/// </summary>
internal readonly struct RequiredTrait(string name, string value)
{
    public string Name { get; } = name;

    public string Value { get; } = value;
}
