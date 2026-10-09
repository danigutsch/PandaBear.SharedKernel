namespace SharedKernel.Versioning.Tool;

internal sealed record CandidateCommandOptions(
    string Root, string? Feed, string? Version, string? Candidate, string? Output, string? Baseline, bool Aot)
{
    public static CandidateCommandOptions Parse(string[] args)
    {
        var root = Environment.CurrentDirectory;
        string? feed = null, version = null, candidate = null, output = null, baseline = null;
        var aot = false;
        var index = 0;
        while (index < args.Length)
        {
            var option = args[index++];
            if (option == "--aot")
            {
                aot = true;
                continue;
            }

            if (index == args.Length)
            {
                throw new ArgumentException($"Missing value for {option}.");
            }

            var value = args[index++];
            switch (option)
            {
                case "--repo-root": root = Path.GetFullPath(value); break;
                case "--feed": feed = Path.GetFullPath(value); break;
                case "--version": version = SemanticVersion.Parse(value).ToString(); break;
                case "--candidate": candidate = Path.GetFullPath(value); break;
                case "--output": output = Path.GetFullPath(value); break;
                case "--baseline-version": baseline = SemanticVersion.Parse(value).ToString(); break;
                default: throw new ArgumentException($"Unknown option: {option}.");
            }
        }

        return new CandidateCommandOptions(root, feed, version, candidate, output, baseline, aot);
    }
}
