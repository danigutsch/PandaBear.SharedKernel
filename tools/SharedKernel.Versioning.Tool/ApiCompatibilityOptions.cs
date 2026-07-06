namespace SharedKernel.Versioning.Tool;

internal sealed record ApiCompatibilityOptions(
    string Version,
    string OutputRoot,
    string ReleasePhase,
    string RepoRoot,
    string? BaselineVersion,
    bool BreakingMarker)
{
    public static ApiCompatibilityOptions Parse(string[] args)
    {
        var version = "0.1.0-alpha.local." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var outputRoot = "artifacts/api-compat";
        var releasePhase = "alpha";
        var repoRoot = ".";
        string? baselineVersion = null;
        var breakingMarker = false;

        var index = 0;
        while (index < args.Length)
        {
            switch (args[index])
            {
                case "--breaking-marker":
                    breakingMarker = true;
                    index++;
                    break;
                case "--version":
                case "--output-root":
                case "--release-phase":
                case "--repo-root":
                    if (index == args.Length - 1)
                    {
                        throw new ArgumentException($"{args[index]} must include a value.");
                    }

                    var option = args[index];
                    var value = args[index + 1];
                    if (option == "--version")
                    {
                        version = value;
                    }
                    else if (option == "--output-root")
                    {
                        outputRoot = value;
                    }
                    else if (option == "--release-phase")
                    {
                        releasePhase = value;
                    }
                    else if (option == "--repo-root")
                    {
                        repoRoot = value;
                    }

                    index += 2;
                    break;
                case "--baseline-version":
                    if (index == args.Length - 1)
                    {
                        throw new ArgumentException($"{args[index]} must include a value.");
                    }

                    baselineVersion = args[index + 1];
                    index += 2;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        return new ApiCompatibilityOptions(version, outputRoot, releasePhase, repoRoot, baselineVersion, breakingMarker);
    }
}
