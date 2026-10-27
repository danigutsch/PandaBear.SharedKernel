namespace PandaBear.RepoConfig.Tool;

/// <summary>GitHub close outcomes a roadmap item can carry when its issue closes.</summary>
internal static class RoadmapCloseReason
{
    /// <summary>Close outcome for an issue that was resolved.</summary>
    public const string Completed = "COMPLETED";
    /// <summary>Close outcome for an issue that was closed without being implemented.</summary>
    public const string NotPlanned = "NOT_PLANNED";

    /// <summary>Returns whether <paramref name="value"/> is one of the two recognized close outcomes.</summary>
    public static bool IsValid(string? value) => value is Completed or NotPlanned;

    /// <summary>Maps a GitHub issue state reason to the close outcome; every reason except NOT_PLANNED counts as completed.</summary>
    public static string FromStateReason(string? stateReason) =>
        string.Equals(stateReason, NotPlanned, StringComparison.Ordinal) ? NotPlanned : Completed;
}
