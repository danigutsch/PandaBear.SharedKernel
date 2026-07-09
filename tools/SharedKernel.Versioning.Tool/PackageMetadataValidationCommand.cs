namespace SharedKernel.Versioning.Tool;

internal static class PackageMetadataValidationCommand
{
    public static void Run(string repositoryRoot, TextWriter output)
    {
        var rootProps = Path.Combine(repositoryRoot, "Directory.Build.props");
        if (!File.Exists(rootProps))
        {
            throw new ArgumentException($"Directory.Build.props does not exist: {rootProps}");
        }

        var failures = new List<string>();
        RequireCentralProperty(rootProps, "Authors", failures);
        RequireCentralProperty(rootProps, "Company", failures);
        RequireCentralProperty(rootProps, "Copyright", failures);
        RequireCentralProperty(rootProps, "PackageLicenseExpression", failures);
        RequireCentralProperty(rootProps, "RepositoryUrl", failures);
        RequireCentralProperty(rootProps, "PublishRepositoryUrl", failures);

        var sharedKernelRoot = Path.Combine(repositoryRoot, "src", "SharedKernel");
        if (!Directory.Exists(sharedKernelRoot))
        {
            throw new ArgumentException($"SharedKernel source directory does not exist: {sharedKernelRoot}");
        }

        foreach (var project in Directory.EnumerateFiles(sharedKernelRoot, "*.csproj", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (IsNonPackable(project))
            {
                continue;
            }

            RequireProjectProperty(project, "PackageId", failures);
            RequireProjectProperty(project, "Description", failures);
            RequireProjectProperty(project, "PackageTags", failures);
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException("Package metadata validation failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        output.WriteLine("Package metadata validation passed.");
    }

    private static void RequireCentralProperty(string project, string propertyName, List<string> failures)
    {
        if (ReadProperty(project, propertyName) is null)
        {
            failures.Add($"- Directory.Build.props missing {propertyName}");
        }
    }

    private static void RequireProjectProperty(string project, string propertyName, List<string> failures)
    {
        if (ReadProperty(project, propertyName) is null)
        {
            failures.Add($"- {Path.GetFileName(project)} missing {propertyName}");
        }
    }

    private static bool IsNonPackable(string project) => string.Equals(ReadProperty(project, "IsPackable"), "false", StringComparison.OrdinalIgnoreCase);

    private static string? ReadProperty(string project, string propertyName) => ProjectPropertyReader.Read(project, propertyName);
}
