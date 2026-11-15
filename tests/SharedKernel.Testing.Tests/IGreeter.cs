namespace PandaBear.Testing.Tests;

/// <summary>A service the registration tests wrap in a <see cref="CallProxy"/>.</summary>
public interface IGreeter
{
    /// <summary>Greets someone.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The greeting.</returns>
    string Greet(string name);
}
