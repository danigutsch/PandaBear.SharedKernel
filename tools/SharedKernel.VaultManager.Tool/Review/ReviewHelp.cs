namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Formats the key list of the review help.</summary>
internal static class ReviewHelp
{
    /// <summary>Formats one line per key: the key, where it works, and what it does.</summary>
    /// <param name="keys">Keys, in the order to show them.</param>
    /// <returns>The lines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keys"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Lines(IReadOnlyList<ReviewKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return
        [
            .. keys.Select(key => key.Key.ToString().PadRight(8) + Where(key.Area).PadRight(11) + key.Action),
            string.Empty,
            "Tab and Shift+Tab move between the search field, the groups, and the tables; arrow keys move inside them.",
        ];
    }

    private static string Where(ReviewArea area) => area switch
    {
        ReviewArea.Groups => "Groups",
        ReviewArea.Fields => "Fields",
        ReviewArea.Findings => "Findings",
        _ => "Anywhere",
    };
}
