namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Verifies that the files in the Git index, staged and in the working tree, use the line endings that their
/// <c>eol</c> attribute requires.
/// </summary>
internal static class LineEndingVerifier
{
    /// <summary>
    /// Reports each index entry whose <c>eol=lf</c> attribute meets CRLF or mixed line endings in the staged blob or
    /// the working-tree file, or whose <c>eol=crlf</c> attribute meets LF or mixed line endings in the working-tree
    /// file. Git stores <c>eol=crlf</c> text with LF in the index, so the staged blob of such a file is not checked.
    /// Entries without an <c>eol</c> attribute, binary content, and entries missing from the working tree are not
    /// checked there.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not list the index in time.</exception>
    public static async Task<IReadOnlyList<RepoConfigIssue>> Verify(string rootPath, CancellationToken cancellationToken)
    {
        var records = await GitTrackedFiles.ReadLineEndings(Path.GetFullPath(rootPath), cancellationToken).ConfigureAwait(false);
        List<RepoConfigIssue> issues = [];
        HashSet<string> reported = new(StringComparer.Ordinal);
        foreach (var record in records)
        {
            // "i/<eol> w/<eol> attr/<attributes>\t<path>"; an unmerged path has a record for each stage.
            var separator = record.IndexOf('\t', StringComparison.Ordinal);
            var fields = separator < 0 ? [] : record[..separator].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3 || !fields[0].StartsWith("i/", StringComparison.Ordinal) || !fields[1].StartsWith("w/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Git index inspection failed.");
            }

            var index = fields[0][2..];
            var workingTree = fields[1][2..];
            var message = Array.IndexOf(fields, "eol=lf") > 0 && (index is "crlf" or "mixed" || workingTree is "crlf" or "mixed")
                ? "Expected LF line endings."
                : Array.IndexOf(fields, "eol=crlf") > 0 && workingTree is "lf" or "mixed"
                    ? "Expected CRLF line endings."
                    : null;
            var path = record[(separator + 1)..];
            if (message is not null && reported.Add(path))
            {
                issues.Add(new RepoConfigIssue(path, message));
            }
        }

        return issues;
    }
}
