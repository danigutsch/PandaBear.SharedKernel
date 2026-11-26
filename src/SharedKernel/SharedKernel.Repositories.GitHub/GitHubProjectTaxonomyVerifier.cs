using System.Globalization;
using System.Text.Json;
using SharedKernel.ConfigurationFiles;

namespace SharedKernel.Repositories.GitHub;

/// <summary>Verifies caller-defined field and issue requirements against a GitHub Project without modifying it.</summary>
public static class GitHubProjectTaxonomyVerifier
{
    private const string ConfigurationPath = ".config/test-taxonomy-project.json";
    private const string Text = "TEXT";
    private const string Number = "NUMBER";
    private const string SingleSelect = "SINGLE_SELECT";

    /// <summary>Reads the repository's Project policy and returns findings for missing or invalid metadata.</summary>
    /// <param name="rootPath">Repository root containing <c>.config/test-taxonomy-project.json</c>.</param>
    /// <param name="connection">Authenticated GitHub connection, which the caller disposes.</param>
    /// <param name="cancellationToken">Cancels the verification.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The policy, authentication, Project target, or remote response is invalid or unavailable.</exception>
    /// <exception cref="IOException">The policy file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to the policy file is denied.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled the verification.</exception>
    public static async Task<IReadOnlyList<VerificationIssue>> Verify(string rootPath, GitHubConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(connection);
        var configuration = await JsonConfigurationFile.Read(
            rootPath, ConfigurationPath, GitHubProjectTaxonomyJsonContext.Default.GitHubProjectTaxonomyConfiguration, cancellationToken).ConfigureAwait(false);
        ValidateConfiguration(configuration);
        var client = new GitHubProjectClient(connection.Client);

        try
        {
            return await VerifyProject(client, configuration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException or IOException or OperationCanceledException)
        {
            throw new InvalidOperationException("GitHub Project taxonomy metadata could not be read. Check the configured Project and read access.", exception);
        }
    }

    private static void ValidateConfiguration(GitHubProjectTaxonomyConfiguration configuration)
    {
        if (configuration.Version != 1)
        {
            throw new InvalidOperationException($"{ConfigurationPath} must use version 1.");
        }

        if (configuration.Target.Number < 1
            || string.IsNullOrWhiteSpace(configuration.Target.Id)
            || string.IsNullOrWhiteSpace(configuration.Target.Owner))
        {
            throw new InvalidOperationException($"{ConfigurationPath} requires a Project id, owner, and positive number.");
        }

        HashSet<string> fieldNames = new(StringComparer.Ordinal);
        foreach (var field in configuration.Fields)
        {
            if (field is null || string.IsNullOrWhiteSpace(field.Name) || field.Name.Any(char.IsControl)
                || field.Type is not (Text or Number or SingleSelect))
            {
                throw new InvalidOperationException($"{ConfigurationPath} requires named fields of type TEXT, NUMBER, or SINGLE_SELECT.");
            }

            if (!fieldNames.Add(field.Name))
            {
                throw new InvalidOperationException($"{ConfigurationPath} contains a duplicate field name.");
            }
        }

        HashSet<string> issueNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in configuration.Issues)
        {
            if (issue is null || issue.Number < 1 || !GitHubRepositoryName.IsValid(issue.Repository))
            {
                throw new InvalidOperationException($"{ConfigurationPath} requires issue references with an owner/name repository and positive number.");
            }

            if (!issueNames.Add(FormatIssue(issue)))
            {
                throw new InvalidOperationException($"{ConfigurationPath} contains a duplicate issue reference.");
            }
        }
    }

    private static async Task<IReadOnlyList<VerificationIssue>> VerifyProject(
        GitHubProjectClient client, GitHubProjectTaxonomyConfiguration configuration, CancellationToken cancellationToken)
    {
        await client.VerifyTarget(configuration.Target, cancellationToken).ConfigureAwait(false);
        var fields = await client.GetFields(configuration.Target, cancellationToken).ConfigureAwait(false);
        List<VerificationIssue> findings = [];
        List<GitHubProjectResponse.ProjectField> requiredFields = [];
        HashSet<string> fieldIds = new(StringComparer.Ordinal);
        foreach (var expected in configuration.Fields)
        {
            var field = expected!;
            var matches = fields.Where(candidate => string.Equals(candidate.Name, field.Name, StringComparison.Ordinal)).ToArray();
            if (matches is not [var match] || string.IsNullOrWhiteSpace(match.Id)
                || !string.Equals(match.DataType, field.Type, StringComparison.Ordinal) || !fieldIds.Add(match.Id))
            {
                findings.Add(new VerificationIssue(ConfigurationPath, $"Project field '{field.Name}' must exist once with type {field.Type}."));
                continue;
            }

            requiredFields.Add(match);
        }

        if (findings.Count > 0)
        {
            return findings;
        }

        HashSet<string> issueIds = new(StringComparer.Ordinal);
        foreach (var expected in configuration.Issues)
        {
            var issue = expected!;
            var name = FormatIssue(issue);
            var issueId = await client.GetIssueNodeId(issue.Repository, issue.Number, cancellationToken).ConfigureAwait(false);
            if (!issueIds.Add(issueId))
            {
                findings.Add(new VerificationIssue(ConfigurationPath, $"{name} resolved to a duplicate Project issue identity."));
                continue;
            }

            var itemId = await client.FindItemId(configuration.Target, issueId, cancellationToken).ConfigureAwait(false);
            if (itemId is null)
            {
                findings.Add(new VerificationIssue(ConfigurationPath, $"{name} is missing from the configured GitHub Project."));
                continue;
            }

            var values = await client.GetFieldValues(itemId, cancellationToken).ConfigureAwait(false);
            foreach (var field in requiredFields)
            {
                var matches = values.Where(value => string.Equals(value.Field?.Id, field.Id, StringComparison.Ordinal)).ToArray();
                if (matches is not [var match] || !IsValidValue(field, match))
                {
                    findings.Add(new VerificationIssue(ConfigurationPath, $"{name} must have one valid {field.DataType} value for Project field '{field.Name}'."));
                }
            }
        }

        return findings;
    }

    private static bool IsValidValue(GitHubProjectResponse.ProjectField field, GitHubProjectItemResponse.FieldValue value) =>
        field.DataType switch
        {
            Text => !string.IsNullOrWhiteSpace(value.Text) && value.Number is null && value.OptionId is null,
            Number => value.Number.HasValue && value.Text is null && value.OptionId is null,
            SingleSelect => value.Number is null && value.Text is null && !string.IsNullOrWhiteSpace(value.OptionId)
                && field.Options?.Count(option => string.Equals(option.Id, value.OptionId, StringComparison.Ordinal)) == 1,
            _ => false,
        };

    private static string FormatIssue(GitHubProjectTaxonomyConfiguration.Issue issue) =>
        string.Create(CultureInfo.InvariantCulture, $"{issue.Repository}#{issue.Number}");
}
