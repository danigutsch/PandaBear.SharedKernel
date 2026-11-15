namespace PandaBear.Testing.Tests;

/// <summary>The real <see cref="IGreeter"/>, which records whether it was disposed.</summary>
internal sealed class TrackedGreeter : IGreeter, IDisposable
{
    /// <summary>Gets a value indicating whether <see cref="Dispose"/> was called.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public string Greet(string name) => $"Hello {name}";

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;
}
