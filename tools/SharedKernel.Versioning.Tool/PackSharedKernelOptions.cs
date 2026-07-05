namespace SharedKernel.Versioning.Tool;

internal sealed record PackSharedKernelOptions(string Version, string OutputRoot, bool VerifyRestore, string RepoRoot)
{
    public static PackSharedKernelOptions Parse(string[] args)
    {
        string? version = null;
        var outputRoot = "artifacts/packages/local";
        var verifyRestore = true;
        var repoRoot = ".";

        var index = 0;
        while (index < args.Length)
        {
            switch (args[index])
            {
                case "--skip-restore-check":
                    verifyRestore = false;
                    index++;
                    break;
                case "--version":
                case "--output-root":
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
                    else
                    {
                        repoRoot = value;
                    }

                    index += 2;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        return new PackSharedKernelOptions(
            version ?? "0.1.0-alpha.local." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture),
            outputRoot,
            verifyRestore,
            repoRoot);
    }
}
