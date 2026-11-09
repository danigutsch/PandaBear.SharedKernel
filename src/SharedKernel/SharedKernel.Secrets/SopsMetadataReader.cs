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
    private const string DefaultUnencryptedSuffix = "_unencrypted";

    private static readonly string[] KnownNames =
    [
        "age", "key_groups", "lastmodified", "mac", "version", "shamir_threshold", "unencrypted_suffix", "encrypted_suffix", "unencrypted_regex", "encrypted_regex",
        "unencrypted_comment_regex", "encrypted_comment_regex", "mac_only_encrypted", "kms", "gcp_kms", "azure_kv", "hc_vault", "pgp", "hckms",
    ];

    private static readonly HashSet<string> ScalarProperties = new(StringComparer.Ordinal) { "lastmodified", "mac", "version", "shamir_threshold" };

    private static readonly HashSet<string> SettingProperties = new(StringComparer.Ordinal)
    {
        "unencrypted_suffix", "encrypted_suffix", "unencrypted_regex", "encrypted_regex", "unencrypted_comment_regex", "encrypted_comment_regex", "mac_only_encrypted",
    };

    private static readonly HashSet<string> KeyHolderProperties = new(StringComparer.Ordinal) { "kms", "gcp_kms", "azure_kv", "hc_vault", "pgp", "hckms" };

    private static readonly HashSet<string> FalseSpellings = new(StringComparer.OrdinalIgnoreCase) { "false", "no", "n", "off", string.Empty };

    /// <summary>Reads the key holders from <paramref name="content"/>.</summary>
    /// <param name="content">The text of the encrypted file.</param>
    /// <param name="format">The document format.</param>
    /// <returns>The key holders, or an error that never echoes file content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public static Result<SopsFileRecipients> Read(string content, SopsFormat format)
    {
        ArgumentNullException.ThrowIfNull(content);
        string json;
        IReadOnlyList<string>? yamlData = null;
        if (format == SopsFormat.Json)
        {
            json = content;
        }
        else
        {
            var converted = SopsYamlSubset.Read(content);
            if (converted is null)
            {
                return Failure("The SOPS metadata is not in the YAML subset that SOPS writes, or the file has no single 'sops' block.");
            }

            (json, var dataLines) = converted.Value;
            yamlData = dataLines;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            var hasComment = false;
            var plain = yamlData is null ? PlainValueScanner.CountJson(document.RootElement) : PlainValueScanner.CountYaml(yamlData, out hasComment);
            return ReadDocument(document.RootElement, plain, hasComment);
        }
        catch (JsonException)
        {
            return Failure("The SOPS metadata is not valid.");
        }
    }

    private static Result<SopsFileRecipients> ReadDocument(JsonElement root, int plainValues, bool hasComment)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure("The file is not a SOPS document.");
        }

        JsonElement? sops = null;
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, "sops", StringComparison.OrdinalIgnoreCase) && !property.NameEquals("sops"))
            {
                return Failure("A top-level property reads as 'sops' without being spelled that way.");
            }

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
        List<string> concerns = [];
        HashSet<string> envelopes = new(StringComparer.Ordinal);
        Dictionary<string, string> labelEnvelopes = new(StringComparer.Ordinal);
        if (!ReadHolders(block, age, other, concerns, envelopes, labelEnvelopes, out var problem))
        {
            return Failure(problem);
        }

        if (plainValues > 0)
        {
            concerns.Add(plainValues == 1 ? "1 value is not encrypted" : $"{plainValues} values are not encrypted");
        }

        if (hasComment)
        {
            concerns.Add("comment present");
        }

        return Result.Ok(new SopsFileRecipients(age, [.. other], concerns, labelEnvelopes));
    }

    private static bool ReadHolders(JsonElement block, List<AgeRecipient> age, SortedSet<string> other, List<string> concerns, HashSet<string> envelopes, Dictionary<string, string> labelEnvelopes, out string problem)
    {
        problem = string.Empty;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var property in block.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                problem = "A property is repeated in the SOPS metadata.";
                return false;
            }

            var value = property.Value;
            var name = KnownNames.FirstOrDefault(known => string.Equals(known, property.Name, StringComparison.OrdinalIgnoreCase)) ?? property.Name;
            switch (name)
            {
                case "age":
                    if (!ReadAge(value, age, envelopes, labelEnvelopes, out problem))
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
                        if (group.ValueKind != JsonValueKind.Object || !ReadHolders(group, age, other, concerns, envelopes, labelEnvelopes, out problem))
                        {
                            problem = problem.Length == 0 ? "A SOPS key group is not a mapping." : problem;
                            return false;
                        }
                    }

                    break;
                default:
                    if (SettingProperties.Contains(name))
                    {
                        if (IsNonDefaultSetting(name, value))
                        {
                            concerns.Add($"non-default setting {name}");
                        }
                    }
                    else if (KeyHolderProperties.Contains(name) || (!ScalarProperties.Contains(name) && IsNonEmptyCollection(value)))
                    {
                        if (IsNonEmptyCollection(value))
                        {
                            other.Add(property.Name);
                        }
                    }
                    else if (!ScalarProperties.Contains(name))
                    {
                        concerns.Add($"unknown property {Shorten(property.Name)}");
                    }

                    break;
            }
        }

        return true;
    }

    private static bool ReadAge(JsonElement value, List<AgeRecipient> age, HashSet<string> envelopes, Dictionary<string, string> labelEnvelopes, out string problem)
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
            string? enc = null;
            HashSet<string> fields = new(StringComparer.Ordinal);
            if (entry.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in entry.EnumerateObject())
                {
                    if (string.Equals(property.Name, "recipient", StringComparison.OrdinalIgnoreCase))
                    {
                        if (recipient is not null || !fields.Add("recipient"))
                        {
                            problem = "A SOPS age entry repeats a field.";
                            return false;
                        }

                        recipient = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    }
                    else if (string.Equals(property.Name, "enc", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!fields.Add("enc"))
                        {
                            problem = "A SOPS age entry repeats a field.";
                            return false;
                        }

                        enc = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    }
                }
            }

            var parsed = AgeRecipient.Create(recipient);
            if (parsed.IsFailure)
            {
                problem = "A SOPS age recipient is missing or is not a valid age recipient.";
                return false;
            }

            if (age.Any(item => item.Equals(parsed.Value)))
            {
                problem = "A SOPS age recipient is listed more than once.";
                return false;
            }

            if (enc is null || !AgeEnvelope.IsSingleStanzaFor(parsed.Value, enc) || !envelopes.Add(enc.Trim()))
            {
                problem = "A SOPS age entry does not carry its own age file with exactly one recipient stanza that suits its label.";
                return false;
            }

            labelEnvelopes[parsed.Value.Value] = enc.Trim();
            age.Add(parsed.Value);
        }

        return true;
    }

    private static bool IsNonDefaultSetting(string name, JsonElement value)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText(),
        };
        return name switch
        {
            "unencrypted_suffix" => text.Length > 0 && text != DefaultUnencryptedSuffix,
            "mac_only_encrypted" => !FalseSpellings.Contains(text),
            _ => text.Length > 0,
        };
    }

    private static string Shorten(string name) => name.Length <= 40 ? name : name[..40] + "...";

    private static bool IsNonEmptyCollection(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.GetArrayLength() > 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => false,
    };

    private static Result<SopsFileRecipients> Failure(string message) => Result.Error<SopsFileRecipients>(message);
}
