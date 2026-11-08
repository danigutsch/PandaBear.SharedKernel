namespace PandaBear.Testing.Tests;

/// <summary>The inherited part of <see cref="ICallProbe"/>; public because proxies can only implement public interfaces.</summary>
public interface ICallProbeBase
{
    /// <summary>Returns a fixed number, from a base interface.</summary>
    /// <returns>A task with the number.</returns>
    Task<int> Inherited();
}
