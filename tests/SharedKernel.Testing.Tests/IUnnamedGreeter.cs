namespace PandaBear.Testing.Tests;

/// <summary>A service that no test names in <see cref="CallProxy.Create"/>, so no proxy is generated for it.</summary>
public interface IUnnamedGreeter
{
    /// <summary>Greets someone.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The greeting.</returns>
    string Greet(string name);
}
