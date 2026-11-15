using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>Checks <see cref="UmamiPageViewOptions"/> when the host starts; a failure names the setting, never a configured value.</summary>
/// <param name="endpointConfigurationKey">The configuration key the endpoint is read from, named in its failure.</param>
internal sealed class UmamiPageViewOptionsValidator(string endpointConfigurationKey) : IValidateOptions<UmamiPageViewOptions>
{
    private const string HeaderTokenSymbols = "!#$%&'*+-.^_`|~";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, UmamiPageViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsBaseAddress(options.Endpoint))
        {
            return ValidateOptionsResult.Fail(
                $"Set {endpointConfigurationKey} to the absolute http or https base address of Umami, such as https://analytics.example.com/, without user info, query or fragment.");
        }

        if (options.Hostname is not null && Uri.CheckHostName(options.Hostname) == UriHostNameType.Unknown)
        {
            return ValidateOptionsResult.Fail($"{nameof(UmamiPageViewOptions.Hostname)} must be a host name such as example.com, without scheme, port or path.");
        }

        if (string.IsNullOrEmpty(options.ClientIpHeader) || !options.ClientIpHeader.All(IsHeaderTokenCharacter))
        {
            return ValidateOptionsResult.Fail($"{nameof(UmamiPageViewOptions.ClientIpHeader)} must be an HTTP header name, such as X-Forwarded-For.");
        }

        if (options.QueueCapacity is < 1 or > UmamiPageViewOptions.MaximumQueueCapacity)
        {
            return ValidateOptionsResult.Fail($"{nameof(UmamiPageViewOptions.QueueCapacity)} must be from 1 to {UmamiPageViewOptions.MaximumQueueCapacity}.");
        }

        if (options.ShutdownTimeout <= TimeSpan.Zero || options.ShutdownTimeout > UmamiPageViewOptions.MaximumShutdownTimeout)
        {
            return ValidateOptionsResult.Fail($"{nameof(UmamiPageViewOptions.ShutdownTimeout)} must be greater than zero and at most {UmamiPageViewOptions.MaximumShutdownTimeout}.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsBaseAddress(Uri? endpoint) =>
        endpoint is { IsAbsoluteUri: true }
        && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps)
        && endpoint.UserInfo.Length == 0
        && endpoint.Query.Length == 0
        && endpoint.Fragment.Length == 0;

    private static bool IsHeaderTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || HeaderTokenSymbols.Contains(character, StringComparison.Ordinal);
}
