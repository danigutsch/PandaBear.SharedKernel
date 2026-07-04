namespace SharedKernel.Versioning.Tool;

internal static class ReleaseImpactText
{
    public static string ToOutputValue(ReleaseImpact impact) => impact switch
    {
        ReleaseImpact.Major => "major",
        ReleaseImpact.Minor => "minor",
        ReleaseImpact.Patch => "patch",
        _ => "none",
    };
}
