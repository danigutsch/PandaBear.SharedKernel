namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The consumer policy in <c>.config/sharedkernel-repo.json</c>.
/// </summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="LicenseExceptions">The license exceptions that <c>dependency verify-license-exceptions</c> verifies, if any.</param>
internal sealed record ConsumerConfiguration(int Version, LicenseExceptionConfiguration? LicenseExceptions = null);
