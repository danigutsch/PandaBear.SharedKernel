namespace PandaBear.Testing;

/// <summary>Temporarily sets a process environment variable and restores its previous value.</summary>
/// <remarks>Dispose nested scopes in reverse order. Serialize tests that change process environment variables.</remarks>
public sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;

    /// <summary>Sets the variable, preserving an unset value for restoration.</summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The temporary value, or null to remove the variable.</param>
    public EnvironmentVariableScope(string name, string? value)
    {
        _name = name;
        _previousValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    /// <summary>Restores the previous value, including an originally unset variable.</summary>
    public void Dispose() => Environment.SetEnvironmentVariable(_name, _previousValue);
}
