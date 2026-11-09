using System.Globalization;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Reads and validates <c>.config/sharedkernel-secrets.json</c> into a <see cref="SopsCreationRuleSet"/>. Errors name the
/// file and the JSON path of the problem and never repeat a value.
/// </summary>
internal static class SecretsConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-secrets.json";

    private const int MaxBytes = 1024 * 1024;

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">The file is missing, larger than 1 MiB, not valid version 1, or a rule is invalid.</exception>
    public static async Task<SopsCreationRuleSet> Read(string root, CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = RepositoryPath.Resolve(root, RelativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{RelativePath}: {exception.Message}", exception);
        }

        byte[] content;
        try
        {
            content = await ReadBounded(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{RelativePath} does not exist.", exception);
        }

        return Parse(content);
    }

    private static SopsCreationRuleSet Parse(byte[] content)
    {
        SecretsConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(content, SecretsConfigurationJsonContext.Default.SecretsConfiguration);
        }
        catch (JsonException exception)
        {
            // The serializer's message names .NET types, so only the location is reported.
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{RelativePath} is not valid at {exception.Path ?? "$"} (line {(exception.LineNumber ?? 0) + 1}). Check for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        if (configuration is null)
        {
            throw new InvalidOperationException($"{RelativePath} must contain a JSON object.");
        }

        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        List<SopsCreationRule> rules = [];
        for (var index = 0; index < configuration.CreationRules.Length; index++)
        {
            rules.Add(ParseRule(index, configuration.CreationRules[index]));
        }

        var result = SopsCreationRuleSet.Create(rules);
        return result.IsSuccess ? result.Value : throw Invalid("$.creationRules is invalid.");
    }

    private static SopsCreationRule ParseRule(int index, CreationRuleConfiguration? entry)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"$.creationRules[{index}]");

        // RespectNullableAnnotations does not cover array elements.
        if (entry?.Recipients is null || entry.PathRegex is null)
        {
            throw Invalid($"{path} must be an object with pathRegex, recipients, and minimumRecipients.");
        }

        List<AgeRecipient> recipients = [];
        for (var position = 0; position < entry.Recipients.Length; position++)
        {
            var recipient = AgeRecipient.Create(entry.Recipients[position]);
            if (recipient.IsFailure)
            {
                throw Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}.recipients[{position}]: {recipient.ErrorDetails?.Detail}"));
            }

            recipients.Add(recipient.Value);
        }

        var set = AgeRecipientSet.Create(recipients);
        if (set.IsFailure)
        {
            throw Invalid($"{path}.recipients: {set.ErrorDetails?.Detail}");
        }

        var rule = SopsCreationRule.Create(entry.PathRegex, set.Value, entry.MinimumRecipients);
        return rule.IsSuccess
            ? rule.Value
            : throw Invalid($"{path}.{rule.ErrorDetails?.ValidationErrors?.Keys.FirstOrDefault() ?? "pathRegex"}: {rule.ErrorDetails?.Detail}");
    }

    private static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");

    private static async Task<byte[]> ReadBounded(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            return length > MaxBytes ? throw new InvalidOperationException($"{RelativePath} is larger than 1 MiB.") : buffer[..length];
        }
    }
}
