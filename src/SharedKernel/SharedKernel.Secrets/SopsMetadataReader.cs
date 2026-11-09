using System.Text.Json;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// Reads the key holders from the <c>sops</c> block of an encrypted YAML or JSON file, without decrypting anything and
/// without a YAML library. It fails closed: a file it does not fully understand is an error, never an empty list.
/// </summary>
/// <remarks>
/// Age recipients come from <c>sops.age[].recipient</c> and from <c>sops.key_groups[].age[].recipient</c>. Any other
/// non-empty key-holder list (such as <c>kms</c>, <c>pgp</c>, or <c>hc_vault</c>) is reported by name in
/// <see cref="SopsFileRecipients.OtherKeyTypes"/>, because its holders can read the file too. A repeated property name in
/// the <c>sops</c> block is an error. YAML is read as the subset that SOPS writes: block mappings and sequences, plain and
/// quoted scalars, block scalars, and empty flow collections.
/// </remarks>
public static class SopsMetadataReader
{
    private static readonly HashSet<string> ScalarProperties = new(StringComparer.Ordinal)
    {
        "lastmodified", "mac", "version", "unencrypted_suffix", "encrypted_suffix", "unencrypted_regex", "encrypted_regex",
        "unencrypted_comment_regex", "encrypted_comment_regex", "mac_only_encrypted", "shamir_threshold",
    };

    /// <summary>Reads the key holders from <paramref name="content"/>.</summary>
    /// <param name="content">The text of the encrypted file.</param>
    /// <param name="format">The document format.</param>
    /// <returns>The key holders, or an error that never echoes file content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public static Result<SopsFileRecipients> Read(string content, SopsFormat format)
    {
        ArgumentNullException.ThrowIfNull(content);
        var json = format == SopsFormat.Json ? content : SopsYamlSubset.ToJson(content);
        if (json is null)
        {
            return Failure("The SOPS metadata is not in the YAML subset that SOPS writes, or the file has no single 'sops' block.");
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            return ReadDocument(document.RootElement);
        }
        catch (JsonException)
        {
            return Failure("The SOPS metadata is not valid.");
        }
    }

    private static Result<SopsFileRecipients> ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure("The file is not a SOPS document.");
        }

        JsonElement? sops = null;
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("sops"))
            {
                if (sops is not null)
                {
                    return Failure("The file has more than one 'sops' block.");
                }

                sops = property.Value;
            }
        }

        if (sops is not { ValueKind: JsonValueKind.Object } block)
        {
            return Failure("The file has no 'sops' block.");
        }

        List<AgeRecipient> age = [];
        SortedSet<string> other = new(StringComparer.Ordinal);
        if (!ReadHolders(block, age, other, out var problem))
        {
            return Failure(problem);
        }

        return Result.Ok(new SopsFileRecipients([.. age.DistinctBy(static item => item.Value, StringComparer.Ordinal)], [.. other]));
    }

    private static bool ReadHolders(JsonElement block, List<AgeRecipient> age, SortedSet<string> other, out string problem)
    {
        problem = string.Empty;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var property in block.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                problem = "A property is repeated in the SOPS metadata.";
                return false;
            }

            var value = property.Value;
            switch (property.Name)
            {
                case "age":
                    if (!ReadAge(value, age, out problem))
                    {
                        return false;
                    }

                    break;
                case "key_groups":
                    if (value.ValueKind != JsonValueKind.Array)
                    {
                        problem = "The SOPS key groups are not a list.";
                        return false;
                    }

                    foreach (var group in value.EnumerateArray())
                    {
                        if (group.ValueKind != JsonValueKind.Object || !ReadHolders(group, age, other, out problem))
                        {
                            problem = problem.Length == 0 ? "A SOPS key group is not a mapping." : problem;
                            return false;
                        }
                    }

                    break;
                default:
                    if (!ScalarProperties.Contains(property.Name) && IsNonEmptyCollection(value))
                    {
                        other.Add(property.Name);
                    }

                    break;
            }
        }

        return true;
    }

    private static bool ReadAge(JsonElement value, List<AgeRecipient> age, out string problem)
    {
        problem = "The SOPS age recipients are not a list of mappings with a 'recipient'.";
        if (value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entry in value.EnumerateArray())
        {
            string? recipient = null;
            if (entry.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in entry.EnumerateObject().Where(static p => p.NameEquals("recipient")))
                {
                    recipient = recipient is null && property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                }
            }

            var parsed = AgeRecipient.Create(recipient);
            if (parsed.IsFailure)
            {
                problem = "A SOPS age recipient is missing or is not a valid age recipient.";
                return false;
            }

            age.Add(parsed.Value);
        }

        return true;
    }

    private static bool IsNonEmptyCollection(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.GetArrayLength() > 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => false,
    };

    private static Result<SopsFileRecipients> Failure(string message) => Result.Error<SopsFileRecipients>(message);
}
