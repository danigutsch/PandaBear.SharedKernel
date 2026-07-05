namespace SharedKernel.Versioning.Tool;

internal sealed record PrepareReleaseOptions(
    string Version,
    string PackageDirectory,
    string OutputDirectory,
    string? SourceTag,
    string? ReleaseImpact,
    string? Sha)
{
    public static PrepareReleaseOptions Parse(string[] args)
    {
        string? version = null;
        string? packageDirectory = null;
        var outputDirectory = "artifacts/release-prep";
        string? sourceTag = null;
        string? releaseImpact = null;
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
                case "--version":
                    version = value;
                    break;
                case "--package-dir":
                    packageDirectory = value;
                    break;
                case "--output-dir":
                    outputDirectory = value;
                    break;
                case "--source-tag":
                    sourceTag = value;
                    break;
                case "--release-impact":
                    releaseImpact = value;
                    break;
                case "--sha":
                    sha = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        return new PrepareReleaseOptions(
            version ?? throw new ArgumentException("--version is required."),
            packageDirectory ?? throw new ArgumentException("--package-dir is required."),
            outputDirectory,
            sourceTag,
            releaseImpact,
            sha);
    }
}
