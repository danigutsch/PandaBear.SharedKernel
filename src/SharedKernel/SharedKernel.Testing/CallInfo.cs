namespace PandaBear.Testing;

/// <summary>One call to a service wrapped by a <see cref="CallProxy"/>.</summary>
/// <param name="method">The method name.</param>
/// <param name="args">The arguments.</param>
public sealed class CallInfo(string method, IReadOnlyList<object?> args)
{
    /// <summary>Gets the method name.</summary>
    public string Method { get; } = method;

    /// <summary>Gets the arguments.</summary>
    public IReadOnlyList<object?> Args { get; } = args;

    /// <summary>Gets the result of the call; set before the after hooks run.</summary>
    public object? Result
    {
        get; internal set;
    }
}
