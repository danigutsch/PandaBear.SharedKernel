using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SharedKernel.ConfigurationFiles;
using SharedKernel.IO;

namespace SharedKernel.Packages;

/// <summary>Prepares changes to a single manager's declaration against an exact file snapshot.</summary>
internal sealed class PackageConfigurationEdit(
    string root,
    byte[] original,
    PackagesConfigurationDocument document,
    PackagesConfiguration configuration)
{
    /// <summary>Gets the validated declaration used to observe installed packages and update decisions.</summary>
    internal PackagesConfiguration Configuration { get; } = configuration;

    /// <summary>Opens the repository's package declaration for editing without changing it.</summary>
    /// <exception cref="InvalidOperationException">The path is linked, or the declaration is missing, oversized or invalid.</exception>
    internal static async Task<PackageConfigurationEdit> Open(string root, CancellationToken cancellationToken)
    {
        byte[]? original;
        try
        {
            original = await BoundedReader.ReadFile(ResolvePath(root), JsonConfigurationFile.MaxBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{PackagesConfiguration.RelativePath} does not exist.", exception);
        }

        if (original is null)
        {
            throw new InvalidOperationException($"{PackagesConfiguration.RelativePath} is larger than 1 MiB.");
        }

        var document = JsonConfigurationFile.Parse(
            original, PackagesConfiguration.RelativePath, PackagesConfigurationJsonContext.Default.PackagesConfigurationDocument);
        return new PackageConfigurationEdit(root, original, document, PackagesConfiguration.Validate(document));
    }

    /// <summary>Returns the existing entries for one manager, in file order.</summary>
    internal IReadOnlyList<string> EntriesFor(PackageManagerKind manager) => document.Packages.GetValueOrDefault(PackageManagers.Id(manager)) ?? [];

    /// <summary>Prepares a validated replacement, or returns null if the declaration does not change.</summary>
    /// <exception cref="InvalidOperationException">An entry is invalid, has another owner, or would make the file oversized.</exception>
    internal byte[]? Prepare(PackageManagerKind manager, string[] entries)
    {
        if (EntriesFor(manager).SequenceEqual(entries, StringComparer.Ordinal))
        {
            return null;
        }

        var packages = new Dictionary<string, string[]>(document.Packages, StringComparer.Ordinal)
        {
            [PackageManagers.Id(manager)] = entries,
        };
        var updated = document with { Packages = packages };
        PackagesConfiguration.Validate(updated);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
        JsonSerializer.Serialize(writer, updated, PackagesConfigurationJsonContext.Default.PackagesConfigurationDocument);
        writer.Flush();
        buffer.GetSpan(1)[0] = (byte)'\n';
        buffer.Advance(1);
        return buffer.WrittenCount <= JsonConfigurationFile.MaxBytes
            ? buffer.WrittenSpan.ToArray()
            : throw new InvalidOperationException($"{PackagesConfiguration.RelativePath} would be larger than 1 MiB.");
    }

    /// <summary>Saves an explicitly confirmed replacement if the original file still matches.</summary>
    /// <exception cref="InvalidOperationException">The path is linked, another edit holds the lock, or the file changed.</exception>
    internal async Task Save(byte[] replacement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(root);
        using var writeLock = AcquireLock(path);
        await VerifyOriginal(cancellationToken).ConfigureAwait(false);

        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File.GetUnixFileMode(path);
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var ownsTemporary = false;
        try
        {
            var output = new FileStream(temporary, options);
            ownsTemporary = true;
            await using (output.ConfigureAwait(false))
            {
                await output.WriteAsync(replacement, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, options.UnixCreateMode.GetValueOrDefault());
            }

            await VerifyOriginal(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (OperatingSystem.IsWindows())
            {
                File.Replace(temporary, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporary, path, overwrite: true);
            }

            ownsTemporary = false;
        }
        finally
        {
            if (ownsTemporary)
            {
                File.Delete(temporary);
            }
        }
    }

    private static FileStream AcquireLock(string path)
    {
        var normalized = OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        try
        {
            return new FileStream(Path.Combine(Path.GetTempPath(), $"sharedkernel-package-edit-{hash}.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException($"Could not lock {PackagesConfiguration.RelativePath} for editing.", exception);
        }
    }

    private async Task VerifyOriginal(CancellationToken cancellationToken)
    {
        var path = ResolvePath(root);
        byte[]? current;
        try
        {
            current = await BoundedReader.ReadFile(path, JsonConfigurationFile.MaxBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw Changed(exception);
        }

        if (current is null || !current.AsSpan().SequenceEqual(original))
        {
            throw Changed();
        }
    }

    private static InvalidOperationException Changed(Exception? inner = null) =>
        new($"{PackagesConfiguration.RelativePath} changed during review. Open the review again before saving.", inner);

    private static string ResolvePath(string root)
    {
        try
        {
            return RepositoryPath.Parse(PackagesConfiguration.RelativePath).ResolveUnder(root);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{PackagesConfiguration.RelativePath}: {exception.Message}", exception);
        }
    }
}
