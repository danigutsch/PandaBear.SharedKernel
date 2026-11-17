using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Checks the <see cref="UmamiPageViewTargetOptions"/> of one connection name when the host starts; a failure names the configuration key
/// or the setting, never a configured value.
/// </summary>
/// <param name="connectionName">The connection name, which is also the options name.</param>
internal sealed class UmamiPageViewTargetOptionsValidator(string connectionName) : IValidateOptions<UmamiPageViewTargetOptions>
{
    private const string HeaderTokenSymbols = "!#$%&'*+-.^_`|~";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, UmamiPageViewTargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.Equals(name, connectionName, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        if (UmamiConnection.Parse(options.ConnectionString) is null)
        {
            return ValidateOptionsResult.Fail(
                $"Set ConnectionStrings:{connectionName} to {UmamiConnection.EndpointKey}=<the absolute http or https base address of Umami, without user info, query or fragment>;"
                + $"{UmamiConnection.WebsiteIdKey}=<the id of the website in Umami, a GUID>, or leave it out to send no page views to Umami.");
        }

        if (string.IsNullOrEmpty(options.ClientIpHeader) || !options.ClientIpHeader.All(IsHeaderTokenCharacter))
        {
            return ValidateOptionsResult.Fail($"{nameof(UmamiPageViewTargetOptions.ClientIpHeader)} must be an HTTP header name, such as X-Forwarded-For.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsHeaderTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || HeaderTokenSymbols.Contains(character, StringComparison.Ordinal);
}
