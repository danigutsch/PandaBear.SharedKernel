namespace SharedKernel.Versioning;

/// <summary>
/// Formats release-impact values for command and artifact output.
/// </summary>
public static class ReleaseImpactText
{
    /// <summary>
    /// Converts a release impact to the stable lowercase output value.
    /// </summary>
    /// <param name="impact">The release impact.</param>
    /// <returns>The output value.</returns>
    public static string ToOutputValue(ReleaseImpact impact) => impact switch
    {
        ReleaseImpact.Major => "major",
        ReleaseImpact.Minor => "minor",
        ReleaseImpact.Patch => "patch",
        _ => "none",
    };
}
