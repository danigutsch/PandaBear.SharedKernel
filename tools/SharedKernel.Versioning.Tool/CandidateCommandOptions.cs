namespace PandaBear.Versioning.Tool;

internal sealed record CandidateCommandOptions(
    string Root, string? Feed, string? Version, string? Candidate, string? Output, string? Baseline, bool Aot)
{
    public static CandidateCommandOptions Parse(string[] args)
    {
        var root = Environment.CurrentDirectory;
        string? feed = null, version = null, candidate = null, output = null, baseline = null;
        string? candidateDirectory = null;
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
                case "--candidate-directory": candidateDirectory = Path.GetFullPath(value); break;
                case "--output": output = Path.GetFullPath(value); break;
                case "--baseline-version": baseline = SemanticVersion.Parse(value).ToString(); break;
                default: throw new ArgumentException($"Unknown option: {option}.");
            }
        }

        if (candidateDirectory is not null)
        {
            if (candidate is not null)
            {
                throw new ArgumentException("--candidate and --candidate-directory cannot be combined.");
            }

            if (!Directory.Exists(candidateDirectory))
            {
                throw new ArgumentException($"Candidate directory does not exist: {candidateDirectory}.");
            }

            var manifests = Directory.EnumerateFiles(candidateDirectory, "candidate.json", SearchOption.AllDirectories)
                .Take(2)
                .ToArray();
            if (manifests.Length != 1)
            {
                throw new ArgumentException("--candidate-directory must contain exactly one candidate.json recursively.");
            }

            candidate = manifests[0];
        }

        return new CandidateCommandOptions(root, feed, version, candidate, output, baseline, aot);
    }
}
