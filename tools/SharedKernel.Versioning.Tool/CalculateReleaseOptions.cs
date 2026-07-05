namespace SharedKernel.Versioning.Tool;

internal sealed record CalculateReleaseOptions(
    string RepoRoot,
    string VersionKind,
    string? RunNumber,
    string? Sha,
    string? GitHubOutput,
    string? GitHubSummary)
{
    public static CalculateReleaseOptions Parse(string[] args)
    {
        var repoRoot = ".";
        var versionKind = "prerelease";
        string? runNumber = null;
        string? sha = null;
        string? githubOutput = null;
        string? githubSummary = null;

        for (var index = 0; index < args.Length; index += 2)
        {
            if (index == args.Length - 1)
            {
                throw new ArgumentException("Every option must include a value.");
            }

            var value = args[index + 1];
            switch (args[index])
            {
                case "--repo-root":
                    repoRoot = value;
                    break;
                case "--version-kind":
                    versionKind = value;
                    break;
                case "--run-number":
                    runNumber = value;
                    break;
                case "--sha":
                    sha = value;
                    break;
                case "--github-output":
                    githubOutput = value;
                    break;
                case "--github-summary":
                    githubSummary = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        if (versionKind is not ("prerelease" or "stable"))
        {
            throw new ArgumentException("--version-kind must be 'prerelease' or 'stable'.");
        }

        return new CalculateReleaseOptions(repoRoot, versionKind, runNumber, sha, githubOutput, githubSummary);
    }
}
