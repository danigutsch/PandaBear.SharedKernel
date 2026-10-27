namespace PandaBear.RepoConfig.Tool;

internal static class RoadmapCloseReason
{
    public const string Completed = "COMPLETED";
    public const string NotPlanned = "NOT_PLANNED";

    public static bool IsValid(string? value) => value is Completed or NotPlanned;

    /// <summary>Maps a GitHub issue state reason to the close outcome; every reason except NOT_PLANNED counts as completed.</summary>
    public static string FromStateReason(string? stateReason) =>
        string.Equals(stateReason, NotPlanned, StringComparison.Ordinal) ? NotPlanned : Completed;
}
