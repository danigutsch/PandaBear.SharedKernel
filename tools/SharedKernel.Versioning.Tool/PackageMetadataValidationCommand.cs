namespace SharedKernel.Versioning.Tool;

internal static class PackageMetadataValidationCommand
{
    public static void Run(string repositoryRoot, TextWriter output)
    {
        PackageMetadataValidator.Validate(repositoryRoot);
        output.WriteLine("Package metadata validation passed.");
    }
}
