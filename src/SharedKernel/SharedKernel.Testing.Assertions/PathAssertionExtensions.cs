namespace SharedKernel.Testing.Assertions;

/// <summary>Asserts the type or absence of a fixture path with a path-specific failure message.</summary>
public static class PathAssertionExtensions
{
    /// <summary>Asserts that the path identifies a file according to <see cref="File.Exists"/>.</summary>
    /// <param name="path">The file path, relative to the current working directory if applicable.</param>
    /// <param name="userMessage">An optional reason to prepend to the failure message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="Xunit.Sdk.TrueException">The path does not identify a file.</exception>
    public static void ShouldExistAsFile(this string path, string? userMessage = null) =>
        RequirePath(path, File.Exists(path), "an existing file", userMessage);

    /// <summary>Asserts that <see cref="File.Exists"/> finds no file at the path.</summary>
    /// <remarks>A missing path or a path to an existing directory satisfies this assertion.</remarks>
    /// <param name="path">The path to check, relative to the current working directory if applicable.</param>
    /// <param name="userMessage">An optional reason to prepend to the failure message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="Xunit.Sdk.TrueException">The path identifies a file.</exception>
    public static void ShouldNotExistAsFile(this string path, string? userMessage = null) =>
        RequirePath(path, !File.Exists(path), "no existing file", userMessage);

    /// <summary>Asserts that the path identifies a directory according to <see cref="Directory.Exists"/>.</summary>
    /// <param name="path">The directory path, relative to the current working directory if applicable.</param>
    /// <param name="userMessage">An optional reason to prepend to the failure message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="Xunit.Sdk.TrueException">The path does not identify a directory.</exception>
    public static void ShouldExistAsDirectory(this string path, string? userMessage = null) =>
        RequirePath(path, Directory.Exists(path), "an existing directory", userMessage);

    /// <summary>Asserts that <see cref="Directory.Exists"/> finds no directory at the path.</summary>
    /// <remarks>A missing path or a path to an existing file satisfies this assertion.</remarks>
    /// <param name="path">The path to check, relative to the current working directory if applicable.</param>
    /// <param name="userMessage">An optional reason to prepend to the failure message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="Xunit.Sdk.TrueException">The path identifies a directory.</exception>
    public static void ShouldNotExistAsDirectory(this string path, string? userMessage = null) =>
        RequirePath(path, !Directory.Exists(path), "no existing directory", userMessage);

    private static void RequirePath(string path, bool satisfied, string expected, string? userMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var diagnostic = $"Expected {expected} at '{path}'.";
        Xunit.Assert.True(satisfied, userMessage is null ? diagnostic : $"{userMessage}{Environment.NewLine}{diagnostic}");
    }
}
