using System.Globalization;

namespace PandaBear.Repository.Tests;

/// <summary>Answers the Git and dotnet commands of <c>local-check</c> for a <see cref="RepositoryTestScope"/>, and fails the ones a test names.</summary>
internal sealed class LocalCheckScript
{
    /// <summary>The object ID that <c>git rev-parse --verify</c> answers with.</summary>
    public const string BaseId = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The whitespace finding that <c>dotnet format</c> prints for a file.</summary>
    public const string Finding = "error WHITESPACE: Fix whitespace formatting. Replace 4 characters with '\\n'.";

    private readonly RepositoryTestScope _repository;

    /// <summary>Creates a script for <paramref name="repository"/>, with a built test assembly and its host for <c>Core.Tests</c>.</summary>
    public LocalCheckScript(RepositoryTestScope repository)
    {
        _repository = repository;
        _repository.WriteFile(TestHost, string.Empty);
        _repository.WriteFile(TestAssembly, string.Empty);
    }

    /// <summary>The path of the built test assembly of <c>Core.Tests</c>, relative to the root.</summary>
    public const string TestAssembly = "tests/Core.Tests/bin/Debug/net10.0/Core.Tests.dll";

    /// <summary>The path of the apphost next to the test assembly, relative to the root; Windows gives it an <c>.exe</c> extension.</summary>
    public static readonly string TestHost = "tests/Core.Tests/bin/Debug/net10.0/Core.Tests" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);

    /// <summary>Gets the changed paths that both <c>git diff</c> commands answer with, separated by NUL.</summary>
    public string Diff { get; init; } = "src/Core/Changed.cs\0";

    /// <summary>Gets the changed paths that <c>git diff HEAD</c> (the working tree) answers with, separated by NUL.</summary>
    public string WorkingDiff
    {
        get;
        init;
    } = string.Empty;

    /// <summary>Gets the paths that <c>git ls-files --others</c> (untracked, not ignored) answers with, separated by NUL.</summary>
    public string Untracked
    {
        get;
        init;
    } = string.Empty;

    /// <summary>Gets what <c>dotnet msbuild -getProperty:TargetPath</c> answers, or <see langword="null"/> for the built test assembly.</summary>
    public string? Evaluation
    {
        get;
        init;
    }

    /// <summary>Gets whether <c>git rev-parse --is-inside-work-tree</c> answers <c>true</c>.</summary>
    public bool InsideWorkTree { get; init; } = true;

    /// <summary>Gets the failure message for a request, or <see langword="null"/> when the request succeeds.</summary>
    public Func<string, string[], string?>? Fail
    {
        get;
        init;
    }

    /// <summary>Answers a request, or throws the failure that <see cref="Fail"/> names, as the real process runner does.</summary>
    public Task<string> Respond(string fileName, string[] arguments, CancellationToken ct)
    {
        if (Fail?.Invoke(fileName, arguments) is { } message)
        {
            throw new InvalidOperationException(message);
        }

        return Task.FromResult(fileName == "git" ? Git(arguments) : Dotnet(arguments));
    }

    private string Git(string[] arguments) => arguments switch
    {
        ["rev-parse", "--is-inside-work-tree"] => InsideWorkTree ? "true" : "false",
        ["rev-parse", "--verify", ..] => BaseId,
        ["rev-parse", ..] => "fixture",
        ["diff", .., "HEAD", "--"] => WorkingDiff,
        ["diff", ..] => Diff,
        ["ls-files", "-z", "--others", ..] => Untracked,
        _ => string.Empty,
    };

    private string Dotnet(string[] arguments) => arguments is ["msbuild", ..]
        ? Evaluation ?? FullPath(_repository.Root, TestAssembly)
        : string.Empty;

    /// <summary>Builds the message of a failed child process, as the real runner words it.</summary>
    public static string Failure(int exitCode, string output) =>
        string.Create(CultureInfo.InvariantCulture, $"Command failed ({exitCode}): tool args{Environment.NewLine}{output}{Environment.NewLine}");

    /// <summary>Returns <paramref name="relative"/> under <paramref name="root"/> as the tool writes it: a full path with the platform separator.</summary>
    public static string FullPath(string root, string relative) => Path.GetFullPath(Path.Combine(root, relative));
}
