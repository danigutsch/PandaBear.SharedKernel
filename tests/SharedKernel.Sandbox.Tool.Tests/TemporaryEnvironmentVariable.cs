namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Sets a process-wide environment variable for the lifetime of the object and puts the original value back on disposal.</summary>
internal sealed class TemporaryEnvironmentVariable : IDisposable
{
    private readonly string _name;
    private readonly string? _original;

    /// <summary>Sets the variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value to set.</param>
    public TemporaryEnvironmentVariable(string name, string? value)
    {
        _name = name;
        _original = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    /// <summary>Changes the value while the object is alive.</summary>
    /// <param name="value">The new value.</param>
    public void Set(string? value) => Environment.SetEnvironmentVariable(_name, value);

    /// <inheritdoc />
    public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
}
