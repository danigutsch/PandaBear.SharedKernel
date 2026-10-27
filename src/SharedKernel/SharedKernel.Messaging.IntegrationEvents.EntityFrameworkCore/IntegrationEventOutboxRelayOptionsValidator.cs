using Microsoft.Extensions.Options;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Rejects relay and consumer options with a non-positive batch size, poll interval, attempt limit, or claim lease duration.</summary>
internal sealed class IntegrationEventOutboxRelayOptionsValidator : IValidateOptions<IntegrationEventOutboxRelayOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IntegrationEventOutboxRelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BatchSize <= 0)
        {
            return ValidateOptionsResult.Fail("Integration event outbox relay batch size must be greater than zero.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("Integration event outbox relay poll interval must be greater than zero.");
        }

        if (options.MaxConsumeAttempts is <= 0)
        {
            return ValidateOptionsResult.Fail("Integration event transport consumer max attempts must be greater than zero when set.");
        }

        return options.ClaimLeaseDuration > TimeSpan.Zero
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Integration event outbox relay claim lease duration must be greater than zero.");
    }
}
