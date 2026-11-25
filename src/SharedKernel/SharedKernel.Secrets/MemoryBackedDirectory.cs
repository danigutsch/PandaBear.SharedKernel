using System.Security.Cryptography;

namespace SharedKernel.Secrets;

/// <summary>Creates and removes the private directory in which <c>edit</c> lets sops and the editor keep plaintext.</summary>
internal static class MemoryBackedDirectory
{
    private static readonly string[] MemoryFileSystems = ["tmpfs", "ramfs"];

    /// <summary>
    /// Creates a new directory that only the owner can use under <paramref name="root"/>, which must be an existing directory,
    /// not a link, on a memory-backed file system, so that nothing written inside it reaches a disk except through swap.
    /// </summary>
    /// <exception cref="InvalidOperationException">The root is not such a directory.</exception>
    public static string Create(string root)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("edit needs a memory-backed file system (tmpfs or ramfs) for the plaintext, which Windows does not offer.");
        }

        if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root) || new DirectoryInfo(root).LinkTarget is not null)
        {
            throw new InvalidOperationException($"The temporary root {root} must be an existing absolute directory that is not a link.");
        }

        if (!MemoryFileSystems.Contains(FileSystemOf(root), StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"The temporary root {root} is not on a memory-backed file system (tmpfs or ramfs); edit refuses to let plaintext reach a disk.");
        }

        var directory = Path.Combine(root, $"sharedkernel-secrets-{RandomNumberGenerator.GetHexString(32, lowercase: true)}");
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return directory;
    }

    /// <summary>
    /// Removes <paramref name="directory"/> and everything in it, if it still exists. An entry an editor left without write
    /// permission is made writable for its owner first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The directory could not be removed, so plaintext may remain in it.</exception>
    public static void Remove(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Retried below with permissions restored.
        }

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).Prepend(directory))
                {
                    File.SetUnixFileMode(child, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Plaintext may remain in {directory}: it could not be removed ({exception.Message}). Remove it yourself.", exception);
        }
    }

    private static string FileSystemOf(string path)
    {
        try
        {
            return new DriveInfo(path).DriveFormat;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return string.Empty;
        }
    }
}
