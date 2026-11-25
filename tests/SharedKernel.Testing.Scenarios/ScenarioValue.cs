namespace SharedKernel.Testing.Scenarios;

/// <summary>
/// Stores one scenario value and fails clearly when a test reads it before setup.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class ScenarioValue<T>
{
    private T? _value;
    private bool _hasValue;

    /// <summary>
    /// Sets the scenario value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Set(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>
    /// Gets the scenario value.
    /// </summary>
    /// <param name="name">The value name used in failure messages.</param>
    /// <returns>The configured value.</returns>
    public T Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_hasValue)
        {
            throw new InvalidOperationException($"Scenario value '{name}' was read before setup.");
        }

        if (_value is null)
        {
            throw new InvalidOperationException($"Scenario value '{name}' was set to null.");
        }

        return _value;
    }
}
