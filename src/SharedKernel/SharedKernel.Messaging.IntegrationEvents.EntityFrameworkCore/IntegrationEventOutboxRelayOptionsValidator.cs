using Microsoft.Extensions.Options;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class IntegrationEventOutboxRelayOptionsValidator : IValidateOptions<IntegrationEventOutboxRelayOptions>
{
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

        return options.ClaimLeaseDuration > TimeSpan.Zero
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Integration event outbox relay claim lease duration must be greater than zero.");
    }
}
