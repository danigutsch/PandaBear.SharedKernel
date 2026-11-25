namespace SharedKernel.Versioning;

/// <summary>
/// Checks SharedKernel package projects for required public API baseline files.
/// </summary>
public static class PublicApiBaselineChecker
{
    /// <summary>
    /// Ensures every SharedKernel project under a repository root has shipped and unshipped baseline files.
    /// </summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <returns>The number of checked projects.</returns>
    /// <exception cref="ArgumentException">Thrown when required directories, projects, or baseline files are missing.</exception>
    public static int EnsureBaselinesPresent(string repoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var sharedKernelDirectory = Path.Combine(repoRoot, "src", "SharedKernel");
        if (!Directory.Exists(sharedKernelDirectory))
        {
            throw new ArgumentException($"SharedKernel directory does not exist: {sharedKernelDirectory}");
        }

        var projects = Directory.GetFiles(sharedKernelDirectory, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (projects.Length == 0)
        {
            throw new ArgumentException($"No SharedKernel projects found under {sharedKernelDirectory}.");
        }

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

        return projects.Length;
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
