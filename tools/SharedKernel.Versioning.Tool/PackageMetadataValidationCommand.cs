namespace PandaBear.Versioning.Tool;

/// <summary>Validates the package metadata of the repository.</summary>
internal static class PackageMetadataValidationCommand
{
    /// <summary>Validates the metadata under <paramref name="repositoryRoot"/> and reports success.</summary>
    public static void Run(string repositoryRoot, TextWriter output)
    {
        PackageMetadataValidator.Validate(repositoryRoot);
        output.WriteLine("Package metadata validation passed.");
    }
}
