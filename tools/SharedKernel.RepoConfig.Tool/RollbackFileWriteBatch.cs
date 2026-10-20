using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.RepoConfig.Tool;

// Each replacement is atomic; managed failures roll back the completed replacements.
// Process termination can leave a completed prefix and is outside this batch's contract.
internal static class RollbackFileWriteBatch
{
    public static void Apply(string lockScope, IReadOnlyList<AtomicFileWrite> writes, Action? verifyPreconditions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockScope);
        ArgumentNullException.ThrowIfNull(writes);

        var normalizedScope = Path.TrimEndingDirectorySeparator(Path.GetFullPath(lockScope));
        ValidateWriteScope(normalizedScope);
        using var writeLock = AcquireLock(normalizedScope);
        verifyPreconditions?.Invoke();
        if (writes.Count == 0)
        {
            return;
        }

        var normalizedWrites = PrepareWrites(normalizedScope, writes);
        ExecuteWrites(normalizedScope, normalizedWrites, verifyPreconditions);
    }

    private static AtomicFileWrite[] PrepareWrites(string normalizedScope, IEnumerable<AtomicFileWrite> writes)
    {
        var normalizedWrites = writes
            .Select(write => write with { Path = Path.GetFullPath(write.Path) })
            .ToArray();
        foreach (var write in normalizedWrites)
        {
            ValidateWritePath(normalizedScope, write.Path);
        }

        var normalizedPaths = normalizedWrites.Select(write => write.Path).ToArray();
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (normalizedPaths.Distinct(pathComparer).Count() != normalizedPaths.Length)
        {
            throw new InvalidOperationException("Atomic write batch paths must be unique.");
        }

        foreach (var write in normalizedWrites)
        {
            VerifyExpectedContent(write);
        }

        return normalizedWrites;
    }

    private static void ExecuteWrites(string normalizedScope, AtomicFileWrite[] normalizedWrites, Action? verifyPreconditions)
    {
        List<StagedWrite> stagedWrites = [];
        try
        {
            StageWrites(normalizedWrites, stagedWrites);
            verifyPreconditions?.Invoke();
            CommitWrites(normalizedScope, stagedWrites);
        }
        catch (Exception exception)
        {
            ThrowIfRollbackFailed(stagedWrites, exception);
            throw;
        }
        finally
        {
            DeleteTemporaryFiles(stagedWrites);
        }

        DeleteBackupFiles(stagedWrites);
    }

    private static void StageWrites(IEnumerable<AtomicFileWrite> writes, List<StagedWrite> stagedWrites)
    {
        foreach (var write in writes)
        {
            stagedWrites.Add(Stage(write));
        }
    }

    private static void CommitWrites(string normalizedScope, IEnumerable<StagedWrite> stagedWrites)
    {
        foreach (var stagedWrite in stagedWrites)
        {
            VerifyExpectedContent(stagedWrite.Write);
            ValidateWritePath(normalizedScope, stagedWrite.Write.Path);
            if (File.Exists(stagedWrite.Write.Path))
            {
                stagedWrite.BackupPath = CreateUniqueSiblingPath(stagedWrite.Write.Path, "bak");
                File.Copy(stagedWrite.Write.Path, stagedWrite.BackupPath);
            }

            if (stagedWrite.Write.ExpectedContent is null)
            {
                File.Move(stagedWrite.TemporaryPath, stagedWrite.Write.Path);
            }
            else
            {
                File.Move(stagedWrite.TemporaryPath, stagedWrite.Write.Path, overwrite: true);
            }

            stagedWrite.Applied = true;
        }
    }

    private static void ThrowIfRollbackFailed(List<StagedWrite> stagedWrites, Exception exception)
    {
        var rollbackFailures = RollBack(stagedWrites);
        if (rollbackFailures.Count > 0)
        {
            rollbackFailures.Insert(0, exception);
            throw new IOException("Atomic write batch failed and could not be fully rolled back.", new AggregateException(rollbackFailures));
        }
    }

    private static void DeleteTemporaryFiles(IEnumerable<StagedWrite> stagedWrites)
    {
        foreach (var stagedWrite in stagedWrites)
        {
            TryDeleteIfExists(stagedWrite.TemporaryPath);
        }
    }

    private static void DeleteBackupFiles(IEnumerable<StagedWrite> stagedWrites)
    {
        foreach (var stagedWrite in stagedWrites)
        {
            TryDeleteIfExists(stagedWrite.BackupPath);
        }
    }

    private static FileStream AcquireLock(string normalizedScope)
    {
        var scopeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedScope)));
        var lockPath = Path.Combine(Path.GetTempPath(), $"sharedkernel-repo-{scopeHash}.lock");
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("Another repository write is already in progress.", exception);
        }
    }

    private static StagedWrite Stage(AtomicFileWrite write)
    {
        var directory = Path.GetDirectoryName(write.Path)
            ?? throw new InvalidOperationException($"Atomic write path has no parent directory: {write.Path}.");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Atomic write directory does not exist: {directory}.");
        }

        var temporaryPath = CreateUniqueSiblingPath(write.Path, "tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(write.Content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            return new StagedWrite(write, temporaryPath);
        }
        catch
        {
            TryDeleteIfExists(temporaryPath);
            throw;
        }
    }

    private static void VerifyExpectedContent(AtomicFileWrite write)
    {
        if (write.ExpectedContent is null)
        {
            if (File.Exists(write.Path))
            {
                throw new InvalidOperationException($"File changed after the write plan was created: {write.Path}.");
            }

            return;
        }

        if (!File.Exists(write.Path)
            || !string.Equals(File.ReadAllText(write.Path), write.ExpectedContent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"File changed after the write plan was created: {write.Path}.");
        }
    }

    private static List<Exception> RollBack(List<StagedWrite> stagedWrites)
    {
        List<Exception> failures = [];
        foreach (var stagedWrite in stagedWrites.AsEnumerable().Reverse())
        {
            try
            {
                if (stagedWrite.Applied && stagedWrite.BackupPath is null)
                {
                    DeleteIfExists(stagedWrite.Write.Path);
                }

                if (stagedWrite.Applied && stagedWrite.BackupPath is not null && File.Exists(stagedWrite.BackupPath))
                {
                    File.Move(stagedWrite.BackupPath, stagedWrite.Write.Path, overwrite: true);
                }
                else if (!stagedWrite.Applied)
                {
                    DeleteIfExists(stagedWrite.BackupPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static string CreateUniqueSiblingPath(string path, string extension) =>
        $"{path}.{Guid.NewGuid():N}.{extension}";

    private static void ValidateWritePath(string normalizedScope, string normalizedPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var relativePath = Path.GetRelativePath(normalizedScope, normalizedPath);
        if (Path.IsPathRooted(relativePath)
            || string.Equals(relativePath, "..", comparison)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", comparison))
        {
            throw new InvalidOperationException($"Atomic write path is outside the repository write scope: {normalizedPath}.");
        }

        var parentPath = Path.GetDirectoryName(normalizedPath) ?? normalizedScope;
        var relativeParent = Path.GetRelativePath(normalizedScope, parentPath);
        var currentPath = normalizedScope;
        foreach (var segment in relativeParent.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (IsSymbolicLink(currentPath))
            {
                throw new InvalidOperationException($"Atomic write path traverses a symbolic link: {normalizedPath}.");
            }
        }

        if (IsSymbolicLink(normalizedPath))
        {
            throw new InvalidOperationException($"Atomic write path is a symbolic link: {normalizedPath}.");
        }
    }

    private static void ValidateWriteScope(string normalizedScope)
    {
        var root = Path.GetPathRoot(normalizedScope)
            ?? throw new InvalidOperationException($"Atomic write scope has no filesystem root: {normalizedScope}.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var currentPath = root;
        foreach (var segment in Path.GetRelativePath(root, normalizedScope)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!IsSymbolicLink(currentPath))
            {
                continue;
            }

            throw new InvalidOperationException(string.Equals(currentPath, normalizedScope, comparison)
                ? $"Atomic write scope must not be a symbolic link: {normalizedScope}."
                : $"Atomic write scope traverses a symbolic link: {normalizedScope}.");
        }
    }

    private static bool IsSymbolicLink(string path)
    {
        string? linkTarget;
        try
        {
            linkTarget = new FileInfo(path).LinkTarget;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            linkTarget = null;
        }

        if (linkTarget is not null)
        {
            return true;
        }

        try
        {
            return new DirectoryInfo(path).LinkTarget is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteIfExists(string? path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }
    }

    private sealed class StagedWrite(AtomicFileWrite write, string temporaryPath)
    {
        public bool Applied { get; set; }

        public string? BackupPath { get; set; }

        public string TemporaryPath { get; } = temporaryPath;

        public AtomicFileWrite Write { get; } = write;
    }
}
