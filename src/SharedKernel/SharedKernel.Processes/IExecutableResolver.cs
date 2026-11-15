namespace PandaBear.Processes;

/// <summary>Finds the executable file that a command name starts.</summary>
public interface IExecutableResolver
{
    /// <summary>Returns the absolute path of the executable named <paramref name="command"/>, or <see langword="null"/> when none is found.</summary>
    /// <param name="command">The command name, without an extension.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="command"/> is <see langword="null"/>, empty, or white-space.</exception>
    string? Find(string command);
}
