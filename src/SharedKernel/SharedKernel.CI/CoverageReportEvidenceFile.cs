namespace SharedKernel.CI;

/// <summary>Identifies the source tree and the reports produced together by a coverage run.</summary>
/// <param name="Tree">The Git index tree that was built and tested.</param>
/// <param name="Reports">Report SHA-256 hashes by path relative to the evidence file.</param>
internal sealed record CoverageReportEvidenceFile(string Tree, Dictionary<string, string> Reports);
