using System.Text.Json;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Reads and writes the versioned decisions file:
/// <c>{"version":2,"groups":[...],"links":[...],"splits":[...],"findings":[...]}</c>. The <c>findings</c> section is
/// optional when reading. Choice kinds and finding decisions are written by name. A pending entry, with the kind or
/// decision <c>Pending</c>, marks what still needs a decision and counts as no decision.
/// </summary>
internal static class DecisionsFile
{
    /// <summary>Format version written by this tool, and the only version it reads.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Reads a decisions file.</summary>
    /// <param name="path">Decisions file.</param>
    /// <returns>The decisions, or <see cref="MergeDecisions.None"/> when the file does not exist.</returns>
    /// <exception cref="InvalidDataException">The file is not a valid decisions file of a supported version.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static MergeDecisions Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return File.Exists(path) ? Deserialize(File.ReadAllText(path)) : MergeDecisions.None;
    }

    /// <summary>
    /// Writes decisions to a file readable and writable only by the current user on Unix, replacing an existing file.
    /// </summary>
    /// <param name="path">Decisions file.</param>
    /// <param name="decisions">Decisions.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void Write(string path, MergeDecisions decisions)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(decisions);

        SecureFile.WriteAllText(path, Serialize(decisions), overwrite: true);
    }

    /// <summary>Serializes decisions.</summary>
    /// <param name="decisions">Decisions.</param>
    /// <returns>Indented JSON.</returns>
    public static string Serialize(MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var document = new DecisionsDocument(CurrentVersion, decisions.Groups, decisions.Overrides.Links, decisions.Overrides.Splits, decisions.Findings);
        return JsonSerializer.Serialize(document, DecisionsJsonContext.Default.DecisionsDocument);
    }

    /// <summary>Parses and validates decisions.</summary>
    /// <param name="json">Decisions file text.</param>
    /// <returns>The decisions, with their pending entries.</returns>
    /// <exception cref="InvalidDataException">The text is not a valid decisions file of a supported version.</exception>
    public static MergeDecisions Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        DecisionsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, DecisionsJsonContext.Default.DecisionsDocument);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The decisions file is not valid: " + exception.Message, exception);
        }

        if (document is null)
        {
            throw new InvalidDataException("The decisions file is empty.");
        }

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException($"The decisions file has version {document.Version}; only version {CurrentVersion} is supported.");
        }

        ValidateGroups(document.Groups);
        IReadOnlyList<ItemFindingDecisions> findings = document.Findings ?? [];
        ValidateFindings(findings);
        return new MergeDecisions(document.Groups, new MatchOverrides(document.Links, document.Splits)) { Findings = findings };
    }

    private static void ValidateGroups(IReadOnlyList<GroupDecision> groups)
    {
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.GroupKey))
            {
                throw new InvalidDataException("A decisions file group has no group key.");
            }

            foreach (var choice in group.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.FieldPath))
                {
                    throw new InvalidDataException($"A choice in group {group.GroupKey} has no field path.");
                }

                if (!Enum.IsDefined(choice.Kind))
                {
                    throw new InvalidDataException($"The choice for {choice.FieldPath} in group {group.GroupKey} is not Pending, Member, or Empty.");
                }

                if (choice.Kind == FieldChoiceKind.Member && string.IsNullOrWhiteSpace(choice.OriginKey))
                {
                    throw new InvalidDataException($"The choice for {choice.FieldPath} in group {group.GroupKey} names no member.");
                }
            }
        }
    }

    private static void ValidateFindings(IReadOnlyList<ItemFindingDecisions> findings)
    {
        foreach (var item in findings)
        {
            if (string.IsNullOrWhiteSpace(item.OriginKey))
            {
                throw new InvalidDataException("A decisions file finding entry has no origin key.");
            }

            foreach (var choice in item.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.FindingId))
                {
                    throw new InvalidDataException($"A finding decision for {item.OriginKey} has no finding ID.");
                }

                if (!Enum.IsDefined(choice.Decision))
                {
                    throw new InvalidDataException($"The decision on finding {choice.FindingId} of {item.OriginKey} is not Pending, Accepted, or Rejected.");
                }
            }
        }
    }
}
