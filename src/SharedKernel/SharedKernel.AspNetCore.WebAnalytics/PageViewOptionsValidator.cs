using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>Checks <see cref="PageViewOptions"/> when the host starts; a failure names the setting, never a configured value.</summary>
internal sealed class PageViewOptionsValidator : IValidateOptions<PageViewOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PageViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Hostname is not null && Uri.CheckHostName(options.Hostname) == UriHostNameType.Unknown)
        {
            return ValidateOptionsResult.Fail($"{nameof(PageViewOptions.Hostname)} must be a host name such as example.com, without scheme, port or path.");
        }

        if (options.QueueCapacity is < 1 or > PageViewOptions.MaximumQueueCapacity)
        {
            return ValidateOptionsResult.Fail($"{nameof(PageViewOptions.QueueCapacity)} must be from 1 to {PageViewOptions.MaximumQueueCapacity}.");
        }

        if (options.ShutdownTimeout <= TimeSpan.Zero || options.ShutdownTimeout > PageViewOptions.MaximumShutdownTimeout)
        {
            return ValidateOptionsResult.Fail($"{nameof(PageViewOptions.ShutdownTimeout)} must be greater than zero and at most {PageViewOptions.MaximumShutdownTimeout}.");
        }

        return ValidateOptionsResult.Success;
    }
}
