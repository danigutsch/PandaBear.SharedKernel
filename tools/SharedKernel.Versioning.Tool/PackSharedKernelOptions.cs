namespace PandaBear.Versioning.Tool;

internal sealed record PackSharedKernelOptions(
    string Version,
    string OutputRoot,
    bool VerifyRestore,
    string RepoRoot,
    string? AssemblyVersion = null,
    string? FileVersion = null,
    string? InformationalVersion = null,
    string? PackageValidationBaselineVersion = null);
