namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Configures the EF Core integration event outbox relay.
/// </summary>
public sealed class IntegrationEventOutboxRelayOptions
{
    /// <summary>
    /// Gets or sets the maximum number of outbox messages claimed per relay batch.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets how long the hosted relay waits after draining available messages.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long a relay claim remains active before another relay may retry the message.
    /// </summary>
    public TimeSpan ClaimLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
}
