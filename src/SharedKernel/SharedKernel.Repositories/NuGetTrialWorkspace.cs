using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>Copies the reviewed Git state and opted-in package inputs to a disposable checkout.</summary>
internal sealed class NuGetTrialWorkspace : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("sharedkernel-nuget-trial-");
    private readonly string fileAppOutputs = "file-app-restore-" + Guid.NewGuid().ToString("N");

    /// <summary>The trial checkout isolated from source files and Git objects.</summary>
    internal string Root => Path.Combine(directory.FullName, "repository");

    /// <summary>Returns a separate restore output directory for each file-based app in the disposable checkout.</summary>
    internal string FileAppRestorePath(string file)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file)));
        return Path.Combine(Root, ".local", fileAppOutputs, key);
    }

    /// <summary>Copies the repository, its tracked pending changes and the explicitly requested local archives.</summary>
    internal async Task Initialize(string source, string revision, IReadOnlyList<string> localSources, CancellationToken ct)
    {
        var tree = await Git(source, ["diff", "--binary", "--full-index", "--no-ext-diff", "--no-textconv", "HEAD", "--"], ct)
            .ConfigureAwait(false);
        await Git(source, ["clone", "--quiet", "--no-hardlinks", "--no-checkout", "--no-tags", "--", source, Root], ct)
            .ConfigureAwait(false);
        await Git(Root, ["-c", $"core.hooksPath={directory.FullName}", "checkout", "--quiet", "--detach", revision], ct)
            .ConfigureAwait(false);
        if (tree.Length != 0)
        {
            var pending = Path.Combine(directory.FullName, "working-tree.patch");
            await File.WriteAllTextAsync(pending, tree, ct).ConfigureAwait(false);
            await Git(Root, ["apply", "--index", "--", pending], ct).ConfigureAwait(false);
        }

        foreach (var relative in localSources)
        {
            CopyFeed(RepositoryPath(source, relative), RepositoryPath(Root, relative), ct);
        }
    }

    /// <summary>Restores tracked trial inputs from the snapshot staged in the private clone.</summary>
    internal async Task ResetCandidate(CancellationToken ct) =>
        _ = await Git(Root, ["restore", "--worktree", "--", "."], ct).ConfigureAwait(false);

    /// <summary>Returns the trial-only diff after checking its changed-file scope.</summary>
    /// <exception cref="InvalidOperationException">The SDK changed a file outside the allowed props and lock scope.</exception>
    internal async Task<string> ReviewPatch(string propsPath, CancellationToken ct)
    {
        var paths = await Git(Root, ["diff", "--no-renames", "--name-only", "-z", "--"], ct).ConfigureAwait(false);
        if (paths.Split('\0', StringSplitOptions.RemoveEmptyEntries).Any(path =>
            !string.Equals(path, propsPath, StringComparison.Ordinal)
            && !string.Equals(Path.GetFileName(path), "packages.lock.json", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The SDK changed files outside the selected central props and existing package locks.");
        }

        var additions = await Git(Root, ["ls-files", "--others", "-z", "--", ":(glob)**/packages.lock.json"], ct)
            .ConfigureAwait(false);
        if (additions.Split('\0', StringSplitOptions.RemoveEmptyEntries).Any(path =>
            string.Equals(Path.GetFileName(path), "packages.lock.json", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The trial created a new package lock. Review the repository lock policy before retrying.");
        }

        return await Git(Root, ["diff", "--binary", "--full-index", "--no-ext-diff", "--no-textconv", "--no-renames", "--"], ct)
            .ConfigureAwait(false);
    }

    /// <summary>Resolves a relative path inside the repository without following symbolic links.</summary>
    /// <exception cref="ArgumentException">The path is not relative or does not identify a file or package-source location.</exception>
    internal static string RepositoryPath(string root, string relative)
    {
        if (Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Trial paths must be relative to the Git checkout.");
        }

        var path = Path.GetFullPath(relative, root);
        var normalized = Path.GetRelativePath(root, path);
        var first = normalized.Split(Path.DirectorySeparatorChar, 2)[0];
        if (first == "." || string.Equals(first, ".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Trial paths must select a repository file or package source.");
        }

        RollbackFileWriteBatch.ValidateWritePath(root, path);
        return path;
    }

    private static void CopyFeed(string source, string destination, CancellationToken ct)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*.nupkg", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        }))
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file);
            var target = RepositoryPath(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static async Task<string> Git(string root, string[] arguments, CancellationToken ct)
    {
        var git = PathExecutableResolver.Default.Find("git")
            ?? throw new InvalidOperationException("Could not resolve the Git executable.");
        var start = new ProcessStartInfo(git)
        {
            WorkingDirectory = root,
            StandardOutputEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add("--no-optional-locks");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        ProcessEnvironment.RemoveGitVariables(start);
        ProcessEnvironment.RemoveForgeTokens(start);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_NO_LAZY_FETCH"] = "1";
        start.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        var result = await new BoundedProcessRunner().Run(start, forward: null, ct).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput : throw new InvalidOperationException("Could not prepare or inspect the isolated Git checkout.");
    }

    /// <inheritdoc />
    public void Dispose() => directory.Delete(recursive: true);
}
