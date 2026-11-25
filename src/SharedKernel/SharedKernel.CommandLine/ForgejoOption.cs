using System.CommandLine;
using SharedKernel.Forgejo;

namespace SharedKernel.CommandLine;

/// <summary>
/// The <c>--forgejo</c> option of a tool: the address of the Forgejo instance that hosts <c>--repository</c> instead of
/// GitHub. Only an address that <see cref="ForgejoApiClient.ApiBase"/> accepts parses: absolute HTTPS without user
/// information, a query, or a fragment.
/// </summary>
public sealed class ForgejoOption : Option<Uri>
{
    /// <summary>The parse error for any other value.</summary>
    public const string InvalidMessage =
        "--forgejo requires an absolute HTTPS address without user information, a query, or a fragment, such as https://codeberg.org.";

    /// <summary>Creates the option.</summary>
    /// <param name="description">The help text, which says what the instance supplies and where its token comes from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> is <see langword="null"/>.</exception>
    public ForgejoOption(string description)
        : base("--forgejo")
    {
        ArgumentNullException.ThrowIfNull(description);
        Description = description;
        HelpName = "url";
        CustomParser = static result =>
        {
            var value = result.Tokens is [.., var token] ? token.Value : string.Empty;
            if (Uri.TryCreate(value, UriKind.Absolute, out var server))
            {
                try
                {
                    ForgejoApiClient.ApiBase(server);
                    return server;
                }
                catch (ArgumentException)
                {
                }
            }

            result.AddError(InvalidMessage);
            return null;
        };
    }
}
