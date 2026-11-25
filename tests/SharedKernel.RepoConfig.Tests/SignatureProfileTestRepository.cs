using SharedKernel.Processes;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Creates synthetic Git histories and disposable OpenSSH keys for the signature command tests.</summary>
internal sealed class SignatureProfileTestRepository : IDisposable
{
    /// <summary>The fixture's protected signer file path.</summary>
    internal const string SignerPath = ".config/allowed_signers";
    private readonly Testing.TemporaryGitRepository _repository = new();
    private readonly TemporaryDirectory _keys = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Initializes the isolated Git repository.</summary>
    internal Task Initialize() => _repository.Initialize(Cancellation);

    /// <summary>Creates a synthetic signing key outside the repository.</summary>
    internal async Task<string> CreateKey(string name)
    {
        var key = Path.Combine(_keys.Path, name);
        var keygen = PathExecutableResolver.Default.Resolve("ssh-keygen");
        var result = await TestProcess.Run(keygen, _keys.Path, ["-q", "-t", "ed25519", "-N", "", "-f", key], Cancellation);
        result.ExitCode.ShouldBe(0, result.StandardError);
        return key;
    }

    /// <summary>Formats one policy entry with the public key and optional lifetime constraints.</summary>
    internal static async Task<string> SignerEntry(string key, string options = "")
    {
        var publicKey = await File.ReadAllTextAsync(key + ".pub", Cancellation);
        return $"fixture@example.test {options}{publicKey.Trim()}\n";
    }

    /// <summary>Writes the fixture signer file in the working tree.</summary>
    internal void WriteSigners(string content) => _repository.WriteText(SignerPath, content);

    /// <summary>Runs a fixture Git command and returns its standard output.</summary>
    internal Task<string> Git(params string[] arguments) => _repository.RunGit(arguments, Cancellation);

    /// <summary>Stages the fixture state and returns the new commit ID after signing if a key was supplied.</summary>
    internal async Task<string> Commit(string? key = null, string? date = null)
    {
        await Git("add", "--all");
        string[] signing = key is null ? ["--no-gpg-sign"] : ["--gpg-sign"];
        string[] options = key is null ? [] : ["-c", "gpg.format=ssh", "-c", $"user.signingkey={key}"];
        var environment = new Dictionary<string, string?>(_repository.GitEnvironment, StringComparer.Ordinal);
        if (date is not null)
        {
            environment["GIT_COMMITTER_DATE"] = date;
        }

        var result = await TestGit.TryRun(_repository.RootPath,
            [.. options, "commit", .. signing, "--allow-empty", "--message", "test: create signature fixture"], Cancellation, environment);
        result.ExitCode.ShouldBe(0, result.StandardError);
        return (await Git("rev-parse", "HEAD")).Trim();
    }

    /// <summary>Creates a shallow clone in a separate disposable directory.</summary>
    internal Task<string> CloneShallow(string directory) => Git("clone", "--depth=1", "--no-local", _repository.RootPath, directory);

    /// <summary>Runs the public RepoConfig command using the specified refs.</summary>
    internal Task<TestProcessResult> Verify(string baseRef, string headRef = "HEAD", bool withoutKeygen = false, string? workTree = null)
    {
        var dotnet = PathExecutableResolver.Default.Resolve("dotnet");
        var tool = typeof(RepoConfigCommandLine).Assembly.Location;
        var environment = new Dictionary<string, string?>(_repository.GitEnvironment, StringComparer.Ordinal);
        if (withoutKeygen)
        {
            environment["PATH"] = _keys.Path;
        }

        return TestProcess.Run(dotnet, workTree ?? _repository.RootPath,
            [tool, "verify-commit-signatures", $"--base={baseRef}", $"--head={headRef}"], Cancellation, environment);
    }

    /// <summary>Removes the disposable repository and separate synthetic key directory.</summary>
    public void Dispose()
    {
        _keys.Dispose();
        _repository.Dispose();
    }
}
