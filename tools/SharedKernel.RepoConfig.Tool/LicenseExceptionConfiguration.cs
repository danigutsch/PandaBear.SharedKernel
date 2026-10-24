namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Package license exceptions and the reviewed values that they are approved for.
/// </summary>
/// <param name="Approval">The repository file and the reviewed values that the exceptions apply to.</param>
/// <param name="Packages">The approved version of each package ID, in the order that the output lists them.</param>
internal sealed record LicenseExceptionConfiguration(LicenseApprovalConfiguration Approval, OrderedDictionary<string, string> Packages);
