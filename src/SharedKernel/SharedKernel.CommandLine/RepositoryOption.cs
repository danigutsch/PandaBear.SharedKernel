using System.CommandLine;
using SharedKernel.Forgejo;

namespace SharedKernel.CommandLine;

/// <summary>
/// The <c>--repository</c> option of a tool: a forge repository as <c>owner/name</c>. The value becomes part of a request
/// address, so the option accepts only what can add neither a path segment, a query, nor a host: two parts of ASCII
/// letters, digits, <c>-</c>, <c>.</c>, and <c>_</c>, neither of which is <c>.</c> or <c>..</c>. That is the form that
/// <see cref="ForgejoRepositoryName"/> accepts and that includes every GitHub repository; the forge client still applies
/// its own, narrower rule.
/// </summary>
public sealed class RepositoryOption : Option<string>
{
    /// <summary>The parse error for a value that is not <c>owner/name</c>.</summary>
    public const string InvalidMessage =
        "--repository requires owner/name: ASCII letters, digits, '-', '.', and '_' in each part, and neither part '.' nor '..'.";

    /// <summary>Creates the option.</summary>
    /// <param name="description">The help text, which names the forge and what the repository is for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> is <see langword="null"/>.</exception>
    public RepositoryOption(string description)
        : base("--repository")
    {
        ArgumentNullException.ThrowIfNull(description);
        Description = description;
        HelpName = "owner/name";
        Validators.Add(static result =>
        {
            if (!ForgejoRepositoryName.TryParse(result.GetValueOrDefault<string>() ?? string.Empty, out _, out _))
            {
                result.AddError(InvalidMessage);
            }
        });
    }
}
