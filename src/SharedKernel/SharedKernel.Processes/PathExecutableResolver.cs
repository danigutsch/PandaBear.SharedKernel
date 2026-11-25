namespace SharedKernel.Processes;

/// <summary>
/// Resolves a command to an executable file in an absolute <c>PATH</c> directory. A bare file name passed to
/// <see cref="System.Diagnostics.Process"/> is also looked up in the application and current directories, and relative
/// <c>PATH</c> entries resolve from the current directory, so a repository could otherwise supply the executable.
/// </summary>
public sealed class PathExecutableResolver : IExecutableResolver
{
    private readonly bool _useProcessEnvironment;
    private readonly string? _path;
    private readonly bool _isWindows;

    /// <summary>Creates a resolver that searches the given directory list.</summary>
    /// <param name="path">The directory list, separated by <see cref="Path.PathSeparator"/>.</param>
    /// <param name="isWindows">Whether to try <c>&lt;command&gt;.exe</c> first and skip the Unix execute-permission check.</param>
    public PathExecutableResolver(string? path, bool isWindows)
    {
        _path = path;
        _isWindows = isWindows;
    }

    private PathExecutableResolver()
    {
        _useProcessEnvironment = true;
    }

    /// <summary>Gets a resolver that reads the process <c>PATH</c> and operating system on every call.</summary>
    public static PathExecutableResolver Default { get; } = new();

    /// <inheritdoc />
    public string? Find(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var path = _useProcessEnvironment ? Environment.GetEnvironmentVariable("PATH") : _path;
        var isWindows = _useProcessEnvironment ? OperatingSystem.IsWindows() : _isWindows;
        return ExecutableSearch.Find(command, path, isWindows);
    }

    /// <summary>
    /// Returns <paramref name="fileName"/> when it is a fully qualified path, otherwise the executable that
    /// <see cref="Find"/> returns.
    /// </summary>
    /// <param name="fileName">A command name or a fully qualified executable path.</param>
    /// <returns>The absolute executable path to start.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="InvalidOperationException">No absolute <c>PATH</c> directory contains the command.</exception>
    public string Resolve(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.IsPathFullyQualified(fileName)
            ? fileName
            : Find(fileName) ?? throw new InvalidOperationException($"Failed to start {fileName}: no absolute PATH directory contains it.");
    }
}
