using System.Globalization;
using SharedKernel.ConfigurationFiles;

namespace SharedKernel.Secrets;

/// <summary>
/// Reads and validates <c>.config/sharedkernel-secrets.json</c> into a <see cref="SopsCreationRuleSet"/>. The file is strict
/// JSON with <c>"version": 1</c>; errors name the file and the JSON path of the problem and never repeat a value.
/// </summary>
public static class SecretsConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-secrets.json";

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The creation rules in the order they are tried.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The file is missing, larger than 1 MiB, not valid version 1, or a rule is invalid.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The path names a directory, or access is denied.</exception>
    public static async Task<SopsCreationRuleSet> Read(string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        var configuration = await JsonConfigurationFile
            .Read(root, RelativePath, SecretsConfigurationJsonContext.Default.SecretsConfiguration, cancellationToken)
            .ConfigureAwait(false);
        return Validate(configuration);
    }

    private static SopsCreationRuleSet Validate(SecretsConfiguration configuration)
    {
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
}
