using System.Text.Json;
using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Reads and writes the versioned decisions file: <c>{"version":1,"groups":[...],"links":[...],"splits":[...]}</c>.</summary>
internal static class DecisionsFile
{
    /// <summary>Format version written by this tool.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Serializes decisions.</summary>
    /// <param name="decisions">Decisions.</param>
    /// <returns>Indented JSON.</returns>
    public static string Serialize(MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var document = new DecisionsDocument(CurrentVersion, decisions.Groups, decisions.Overrides.Links, decisions.Overrides.Splits);
        return JsonSerializer.Serialize(document, DecisionsJsonContext.Default.DecisionsDocument);
    }

    /// <summary>Parses and validates decisions.</summary>
    /// <param name="json">Decisions file text.</param>
    /// <returns>The decisions.</returns>
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

        foreach (var group in document.Groups)
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

                if (choice.Kind == FieldChoiceKind.Member && string.IsNullOrWhiteSpace(choice.OriginKey))
                {
                    throw new InvalidDataException($"The choice for {choice.FieldPath} in group {group.GroupKey} names no member.");
                }
            }
        }

        return new MergeDecisions(document.Groups, new MatchOverrides(document.Links, document.Splits));
    }
}
