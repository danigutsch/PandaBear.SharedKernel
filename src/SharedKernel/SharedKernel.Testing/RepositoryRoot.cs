namespace PandaBear.Testing;

/// <summary>Finds the root of the repository that a test assembly was built from, by walking up to a marker file.</summary>
public static class RepositoryRoot
{
    /// <summary>Returns the nearest directory at or above the test's base directory that holds <paramref name="marker"/>.</summary>
    /// <param name="marker">A file name, or a file path relative to the root such as <c>src/Directory.Build.props</c>, that marks the root.</param>
    /// <returns>The full path of the directory that holds the marker.</returns>
    /// <exception cref="ArgumentException"><paramref name="marker"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="DirectoryNotFoundException">No directory at or above the base directory holds the marker.</exception>
    public static string Locate(string marker) => Locate(marker, AppContext.BaseDirectory);

    /// <summary>Returns the nearest directory at or above <paramref name="startDirectory"/> that holds <paramref name="marker"/>.</summary>
    /// <param name="marker">A file name, or a file path relative to the root such as <c>src/Directory.Build.props</c>, that marks the root.</param>
    /// <param name="startDirectory">The directory the walk starts at.</param>
    /// <returns>The full path of the directory that holds the marker.</returns>
    /// <exception cref="ArgumentException"><paramref name="marker"/> or <paramref name="startDirectory"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="DirectoryNotFoundException">No directory at or above <paramref name="startDirectory"/> holds the marker.</exception>
    public static string Locate(string marker, string startDirectory) =>
        TryLocate(marker, startDirectory)
        ?? throw new DirectoryNotFoundException($"{marker} was not found in {startDirectory} or any directory above it.");

    /// <summary>Returns the nearest directory at or above the test's base directory that holds <paramref name="marker"/>, or <see langword="null"/>.</summary>
    /// <param name="marker">A file name, or a file path relative to the root such as <c>src/Directory.Build.props</c>, that marks the root.</param>
    /// <returns>The full path of the directory that holds the marker, or <see langword="null"/> when none does.</returns>
    /// <exception cref="ArgumentException"><paramref name="marker"/> is <see langword="null"/>, empty, or white space.</exception>
    public static string? TryLocate(string marker) => TryLocate(marker, AppContext.BaseDirectory);

    /// <summary>Returns the nearest directory at or above <paramref name="startDirectory"/> that holds <paramref name="marker"/>, or <see langword="null"/>.</summary>
    /// <param name="marker">A file name, or a file path relative to the root such as <c>src/Directory.Build.props</c>, that marks the root.</param>
    /// <param name="startDirectory">The directory the walk starts at.</param>
    /// <returns>The full path of the directory that holds the marker, or <see langword="null"/> when none does.</returns>
    /// <exception cref="ArgumentException"><paramref name="marker"/> or <paramref name="startDirectory"/> is <see langword="null"/>, empty, or white space.</exception>
    public static string? TryLocate(string marker, string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, marker)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
