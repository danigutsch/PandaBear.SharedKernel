namespace SharedKernel.Testing.Tests;

/// <summary>The real <see cref="IUnnamedGreeter"/>.</summary>
internal sealed class UnnamedGreeter : IUnnamedGreeter
{
    /// <inheritdoc />
    public string Greet(string name) => $"Hello {name}";
}
