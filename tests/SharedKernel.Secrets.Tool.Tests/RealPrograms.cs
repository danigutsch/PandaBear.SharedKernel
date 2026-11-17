namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Finds age, age-keygen, and sops in the absolute directories of PATH, so integration tests can skip when they are absent.</summary>
internal static class RealPrograms
{
    /// <summary>Gets a value indicating whether <c>age</c>, <c>age-keygen</c>, and <c>sops</c> are all installed.</summary>
    public static bool AllAvailable => Find("age") is not null && Find("age-keygen") is not null && Find("sops") is not null;

    /// <summary>Gets a value indicating whether the programs are installed and <c>/dev/shm</c> is a tmpfs, which <c>edit</c> needs.</summary>
    public static bool EditAvailable => AllAvailable && MemoryRoot.Available;

    /// <summary>Gets a value indicating whether <c>git</c> is installed.</summary>
    public static bool GitAvailable => Find("git") is not null;

    /// <summary>Runs git with no user configuration in <paramref name="directory"/> and returns its exit code.</summary>
    public static async Task<int> Git(string directory, CancellationToken cancellationToken, params string[] arguments)
    {
        var environment = new Dictionary<string, string> { ["GIT_CONFIG_GLOBAL"] = "/dev/null", ["GIT_CONFIG_NOSYSTEM"] = "1" };
        using var result = await new SystemToolRunner().Run(new ToolRequest(Find("git")!, ["-C", directory, "-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", .. arguments], environment), cancellationToken);
        return result.ExitCode;
    }

    /// <summary>Returns the absolute path of <paramref name="command"/> in an absolute PATH directory, or <see langword="null"/>.</summary>
    public static string? Find(string command)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, command);
            if (Path.IsPathFullyQualified(directory) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Returns the recipient that <c>keygen</c> printed on its <c>Public recipient:</c> line.</summary>
    public static string RecipientFrom(string output)
    {
        const string prefix = "Public recipient: ";
        var line = output.Split('\n').First(static l => l.StartsWith(prefix, StringComparison.Ordinal));
        return line[prefix.Length..].Trim();
    }
}
