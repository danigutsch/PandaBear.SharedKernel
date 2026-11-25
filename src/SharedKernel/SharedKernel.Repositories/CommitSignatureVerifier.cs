using System.Globalization;
using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>Verifies a change's Git commits with the SSH signing keys authorized by its protected base.</summary>
public static class CommitSignatureVerifier
{
    /// <summary>The owner-editable signer file seeded by the scaffold and read from the protected base.</summary>
    internal const string SignerPath = ".config/allowed_signers";
    private const int MaxOutputBytes = 1024 * 1024;

    /// <summary>
    /// Reads the signers from <paramref name="baseRef"/> and verifies every commit reachable from
    /// <paramref name="headRef"/> outside the base history.
    /// </summary>
    /// <param name="directory">The directory from which to resolve the Git repository.</param>
    /// <param name="baseRef">The protected base ref supplied by the caller.</param>
    /// <param name="headRef">The submitted change head supplied by the caller.</param>
    /// <param name="output">The writer for the successful verification count.</param>
    /// <param name="cancellationToken">Cancels Git verification and cleans up the temporary signer file.</param>
    /// <returns>Zero after verifying a nonempty commit range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The range or signer file is invalid, a prerequisite is unavailable, or a signature fails verification.</exception>
    /// <exception cref="IOException">The temporary signer file cannot be written or removed.</exception>
    /// <exception cref="TimeoutException">A Git command runs beyond its time limit.</exception>
    public static async Task<int> Run(
        string directory, string baseRef, string headRef, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        var keygen = PathExecutableResolver.Default.Find("ssh-keygen") ?? throw Failure("ssh-keygen is required.");
        var shallow = await RunGit(directory, ["rev-parse", "--is-shallow-repository"], cancellationToken).ConfigureAwait(false);
        if (shallow.ExitCode != 0)
        {
            throw Failure("signature checks require a Git repository.");
        }

        if (ReadText(shallow) != "false")
        {
            throw Failure("a complete Git history is required.");
        }

        var baseline = await ResolveCommit(directory, baseRef, "base", cancellationToken).ConfigureAwait(false);
        var head = await ResolveCommit(directory, headRef, "head", cancellationToken).ConfigureAwait(false);
        var history = await RunGit(directory, ["rev-list", "--reverse", $"{baseline}..{head}"], cancellationToken).ConfigureAwait(false);
        if (history.ExitCode != 0)
        {
            throw Failure("could not read the commit range.");
        }

        var commits = ReadText(history).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (commits.Length == 0)
        {
            throw Failure("no commits to verify.");
        }

        var signers = await RunGit(directory, ["cat-file", "blob", $"{baseline}:{SignerPath}"], cancellationToken).ConfigureAwait(false);
        if (signers.ExitCode != 0 || signers.StandardOutput.IsEmpty)
        {
            throw Failure($"base commit must contain a non-empty {SignerPath}.");
        }

        var temporary = Directory.CreateTempSubdirectory("sharedkernel-commit-signatures-");
        try
        {
            var allowedSignersPath = Path.Combine(temporary.FullName, "allowed_signers");
            await File.WriteAllBytesAsync(allowedSignersPath, signers.StandardOutput, cancellationToken).ConfigureAwait(false);
            var unsupportedFormatProgram = Path.Combine(temporary.FullName, "unsupported-signature-format");
            string[] settings = [
                "-c", $"gpg.ssh.allowedSignersFile={allowedSignersPath}",
                "-c", $"gpg.ssh.program={keygen}",
                "-c", $"gpg.openpgp.program={unsupportedFormatProgram}",
                "-c", $"gpg.x509.program={unsupportedFormatProgram}",
                "-c", "gpg.minTrustLevel=fully",
            ];
            foreach (var commit in commits)
            {
                if (!IsObjectId(commit))
                {
                    throw Failure("Git returned an invalid commit identity.");
                }

                var verification = await RunGit(directory, [.. settings, "verify-commit", commit], cancellationToken).ConfigureAwait(false);
                if (verification.ExitCode != 0)
                {
                    throw Failure($"commit {commit} does not have an allowed SSH signature.");
                }
            }
        }
        finally
        {
            temporary.Delete(recursive: true);
        }

        await output.WriteLineAsync(
            $"Verified {commits.Length.ToString(CultureInfo.InvariantCulture)} commit(s).".AsMemory(), cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async Task<string> ResolveCommit(string directory, string reference, string name, CancellationToken ct)
    {
        var result = await RunGit(directory, ["rev-parse", "--verify", "--end-of-options", $"{reference}^{{commit}}"], ct).ConfigureAwait(false);
        var commit = ReadText(result);
        return result.ExitCode == 0 && IsObjectId(commit)
            ? commit
            : throw Failure($"{name} ref does not resolve to a commit.");
    }

    private static Task<CapturedProcessOutput> RunGit(string directory, string[] arguments, CancellationToken ct)
    {
        var git = GitProcess.Default;
        var start = git.CreateStartInfo(directory, arguments);
        start.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        start.Environment["GIT_NO_LAZY_FETCH"] = "1";
        return git.Run(start, MaxOutputBytes, ReadOnlyMemory<byte>.Empty, ct);
    }

    private static string ReadText(CapturedProcessOutput result) => Encoding.ASCII.GetString(result.StandardOutput.Span).TrimEnd('\r', '\n');

    private static bool IsObjectId(string text) => text.Length is 40 or 64 && text.All(Uri.IsHexDigit);

    private static InvalidOperationException Failure(string message) => new(message);
}
