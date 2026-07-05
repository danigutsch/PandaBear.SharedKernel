namespace SharedKernel.Versioning.Tool;

internal static class PublicApiBaselineCommand
{
    public static async Task Run(string repoRoot, TextWriter output)
    {
        var sharedKernelDirectory = Path.Combine(repoRoot, "src", "SharedKernel");
        var projects = Directory.GetFiles(sharedKernelDirectory, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missing = new List<string>();

        foreach (var project in projects)
        {
            var projectDirectory = Path.GetDirectoryName(project) ?? throw new ArgumentException($"Invalid project path: {project}");
            AddMissingBaseline(project, projectDirectory, "PublicAPI.Shipped.txt", repoRoot, missing);
            AddMissingBaseline(project, projectDirectory, "PublicAPI.Unshipped.txt", repoRoot, missing);
        }

        if (missing.Count > 0)
        {
            throw new ArgumentException("Public API baseline check failed: " + string.Join("; ", missing));
        }

        await output.WriteLineAsync("Public API baselines are present for SharedKernel projects.").ConfigureAwait(false);
    }

    private static void AddMissingBaseline(string project, string projectDirectory, string fileName, string repoRoot, List<string> missing)
    {
        if (File.Exists(Path.Combine(projectDirectory, fileName)))
        {
            return;
        }

        missing.Add(Path.GetRelativePath(repoRoot, project) + ": missing " + fileName);
    }
}
