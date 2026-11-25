namespace SharedKernel.Repositories;

/// <summary>Validation problem found in a configuration file.</summary>
/// <param name="Path">Relative path of the file.</param>
/// <param name="Message">Description of the problem.</param>
public sealed record VerificationIssue(string Path, string Message);
