namespace SharedKernel.Versioning.Tool;

internal sealed record VersionToolOptions(string BaseVersion, string? Since, string? Prerelease, string? Sha)
{
    public static VersionToolOptions Parse(string[] args)
    {
        string? baseVersion = null;
        string? since = null;
        string? prerelease = null;
        string? sha = null;

        for (var index = 0; index < args.Length; index += 2)
        {
            if (index == args.Length - 1)
            {
                throw new ArgumentException("Every option must include a value.");
            }

            var value = args[index + 1];
            switch (args[index])
            {
                case "--base":
                    baseVersion = value;
                    break;
                case "--since":
                    since = value;
                    break;
                case "--prerelease":
                    prerelease = value;
                    break;
                case "--sha":
                    sha = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        return new VersionToolOptions(baseVersion ?? throw new ArgumentException("--base is required."), since, prerelease, sha);
    }
}
