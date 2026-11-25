namespace SharedKernel.Repositories;

/// <summary>The outcome of <see cref="LicenseExceptionVerifier.Verify(string, CancellationToken)"/>.</summary>
/// <param name="Issues">The approval and lock-file mismatches.</param>
/// <param name="PackageUrls">The approved packages as <c>pkg:nuget/&lt;id&gt;@&lt;version&gt;</c>, in configuration order.</param>
public sealed record LicenseExceptionVerification(IReadOnlyList<VerificationIssue> Issues, IReadOnlyList<string> PackageUrls);
