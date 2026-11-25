namespace PandaBear.Repositories;

/// <summary>The scope and outcome of a CI execution manifest for the compared revision.</summary>
/// <param name="Status">The overall result of the verification run.</param>
/// <param name="Slices">The verification slices that ran.</param>
/// <param name="Restore">The restore result, if recorded.</param>
/// <param name="Build">The build result, if recorded.</param>
/// <param name="Test">The test result, if recorded.</param>
internal sealed record NuGetRestoreVerification(string Status, string[] Slices, string? Restore, string? Build, string? Test);
