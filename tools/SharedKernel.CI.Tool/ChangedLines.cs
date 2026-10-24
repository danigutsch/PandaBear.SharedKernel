using System.Globalization;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Reads the added lines of each changed file from a unified diff with zero context lines.</summary>
internal static partial class ChangedLines
{
    /// <summary>
    /// Returns the added line numbers, in the new file, of each changed file that <paramref name="isIncluded"/> accepts.
    /// Deleted files and files without an added line are left out.
    /// </summary>
    /// <param name="patch">The output of <c>git diff --unified=0 --dst-prefix=b/</c> with unquoted paths.</param>
    /// <param name="isIncluded">Selects the repository-relative paths whose lines count.</param>
    /// <exception cref="InvalidDataException">
    /// The patch is not a unified diff, a hunk is malformed or incomplete, or a path is quoted or not repository-relative.
    /// </exception>
    public static Dictionary<string, HashSet<int>> Parse(string patch, Func<string, bool> isIncluded)
    {
        // Lines end only at '\n', so a carriage return inside a changed line cannot split it.
        var lines = patch.Split('\n');
        if (lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }

        var changes = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        string? path = null;
        var next = 0;
        var remaining = 0;
        var removedRemaining = 0;
        var inHunk = false;
        var hasFileHeader = false;
        var awaitingHunk = false;
        foreach (var text in lines)
        {
            if (text.StartsWith("+++ ", StringComparison.Ordinal) && (!inHunk || (remaining == 0 && removedRemaining == 0)))
            {
                if (remaining != 0 || removedRemaining != 0 || awaitingHunk)
                {
                    throw new InvalidDataException("The diff has an incomplete hunk.");
                }

                hasFileHeader = true;
                awaitingHunk = true;
                var name = text[4..].TrimEnd('\r').Split('\t', 2)[0];
                if (name.StartsWith('"'))
                {
                    throw new InvalidDataException("The diff quotes the path " + name + "; coverage reads only paths that Git does not quote.");
                }

                path = name == "/dev/null" ? null : name.StartsWith("b/", StringComparison.Ordinal) ? name[2..] : name;
                if (path is not null && !RepositoryPath.IsRelative(path))
                {
                    throw new InvalidDataException("The diff names a path outside the repository: " + name + ".");
                }

                inHunk = false;
                continue;
            }

            if (text.StartsWith("@@", StringComparison.Ordinal))
            {
                if (remaining != 0 || removedRemaining != 0)
                {
                    throw new InvalidDataException("The diff has an incomplete hunk.");
                }

                if (!hasFileHeader)
                {
                    throw new InvalidDataException("The diff has a hunk without a file header.");
                }

                awaitingHunk = false;
                var match = Hunk().Match(text);
                if (!match.Success
                    || !int.TryParse(match.Groups[3].Value, CultureInfo.InvariantCulture, out next)
                    || !TryCount(match.Groups[4], out remaining)
                    || !TryCount(match.Groups[2], out removedRemaining))
                {
                    throw new InvalidDataException("The diff has a malformed hunk header: " + text.TrimEnd('\r') + ".");
                }

                if ((remaining > 0 && next == 0) || (removedRemaining > 0 && match.Groups[1].Value == "0"))
                {
                    throw new InvalidDataException("The diff has a hunk whose lines do not start at one.");
                }

                inHunk = true;
                if (path is not null && isIncluded(path))
                {
                    changes.TryAdd(path, []);
                }

                continue;
            }

            if (!inHunk || text.StartsWith("\\ No newline", StringComparison.Ordinal))
            {
                continue;
            }

            if (remaining == 0 && removedRemaining == 0)
            {
                inHunk = false;
                continue;
            }

            if (text.StartsWith('+'))
            {
                if (path is not null && isIncluded(path))
                {
                    changes[path].Add(next);
                }

                next++;
                remaining--;
            }
            else if (text.StartsWith('-'))
            {
                removedRemaining--;
            }
            else if (text.StartsWith(' '))
            {
                next++;
                remaining--;
                removedRemaining--;
            }
            else
            {
                throw new InvalidDataException("The diff has a hunk line that is not added, removed, or context.");
            }

            if (remaining < 0 || removedRemaining < 0)
            {
                throw new InvalidDataException("The diff has a hunk whose length does not match its header.");
            }
        }

        if (remaining != 0 || removedRemaining != 0 || awaitingHunk)
        {
            throw new InvalidDataException("The diff has an incomplete hunk.");
        }

        if (lines.Length > 0 && !lines.Any(static line => line.StartsWith("diff --git ", StringComparison.Ordinal) || line.StartsWith("--- ", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The diff is not a unified diff.");
        }

        foreach (var empty in changes.Where(static pair => pair.Value.Count == 0).Select(static pair => pair.Key).ToArray())
        {
            changes.Remove(empty);
        }

        return changes;
    }

    private static bool TryCount(Group group, out int count)
    {
        count = 1;
        return !group.Success || int.TryParse(group.Value, CultureInfo.InvariantCulture, out count);
    }

    [GeneratedRegex(@"\A@@ -([0-9]+)(?:,([0-9]+))? \+([0-9]+)(?:,([0-9]+))? @@", RegexOptions.CultureInvariant)]
    private static partial Regex Hunk();
}
