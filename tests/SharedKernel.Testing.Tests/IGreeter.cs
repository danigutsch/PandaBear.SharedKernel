namespace PandaBear.Testing.Tests;

/// <summary>A service the registration tests wrap in a <see cref="CallProxy"/>; public because proxies can only implement public interfaces.</summary>
public interface IGreeter
{
    /// <summary>Greets someone.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The greeting.</returns>
    string Greet(string name);
}
