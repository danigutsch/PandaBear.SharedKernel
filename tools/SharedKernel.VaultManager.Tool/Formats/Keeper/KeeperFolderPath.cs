namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Converts Keeper folder paths to folder paths with <c>/</c> separators.</summary>
internal static class KeeperFolderPath
{
    /// <summary>Joins a shared folder and the folder inside it.</summary>
    /// <param name="folder">Keeper folder placement.</param>
    /// <returns>Folder path, or <see langword="null"/> when both parts are empty.</returns>
    public static string? Join(KeeperFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        string[] components = [.. Components(folder.SharedFolder), .. Components(folder.Folder)];
        return components.Length == 0 ? null : string.Join('/', components);
    }

    /// <summary>
    /// Splits a Keeper path on single backslashes. A doubled backslash is a literal backslash. A <c>/</c> inside a name
    /// becomes <c>-</c>, as Bitwarden's Keeper importer does, because <c>/</c> separates folder levels.
    /// </summary>
    /// <param name="path">Keeper path, or <see langword="null"/>.</param>
    /// <returns>Non-empty path components.</returns>
    public static IReadOnlyList<string> Components(string? path)
    {
        var components = new List<string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            return components;
        }

        var current = new System.Text.StringBuilder();
        for (int index = 0; index < path.Length; index++)
        {
            char character = path[index];
            if (character == '\\' && index + 1 < path.Length && path[index + 1] == '\\')
            {
                current.Append('\\');
                index++;
            }
            else if (character == '\\')
            {
                Add(components, current);
            }
            else
            {
                current.Append(character == '/' ? '-' : character);
            }
        }

        Add(components, current);
        return components;
    }

    private static void Add(List<string> components, System.Text.StringBuilder current)
    {
        string component = current.ToString().Trim();
        if (component.Length > 0)
        {
            components.Add(component);
        }

        current.Clear();
    }
}
