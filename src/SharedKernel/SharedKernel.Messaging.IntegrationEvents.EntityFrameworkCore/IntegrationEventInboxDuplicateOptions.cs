using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Stores the optional observation mapping for one inbox context.</summary>
/// <typeparam name="TContext">The context that owns the inbox.</typeparam>
internal sealed class IntegrationEventInboxDuplicateOptions<TContext>
    where TContext : DbContext
{
    private readonly Dictionary<string, string> bindings = new(StringComparer.Ordinal);

    /// <summary>Gets the observation table name.</summary>
    public string TableName { get; private set; } = "integration_event_inbox_duplicates";

    /// <summary>Binds an exclusive scope prefix and its consumer to the owning context's table.</summary>
    /// <exception cref="InvalidOperationException">A different table or overlapping scope prefix is already configured.</exception>
    internal void Bind(string consumerName, string scopePrefix, string tableName)
    {
        if (bindings.Count > 0 && !string.Equals(TableName, tableName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Inbox duplicates require one observation table per context.");
        }

        if (bindings.TryGetValue(scopePrefix, out var current) && string.Equals(current, consumerName, StringComparison.Ordinal))
        {
            return;
        }

        if (bindings.Keys.Any(prefix => scopePrefix.StartsWith(prefix, StringComparison.Ordinal)
            || prefix.StartsWith(scopePrefix, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Inbox duplicate consumer scope prefixes cannot overlap.");
        }

        TableName = tableName;
        bindings.Add(scopePrefix, consumerName);
    }

    /// <summary>Returns the sole registered consumer matching the scope, or null when it is outside all registrations.</summary>
    internal string? FindConsumer(string scope)
    {
        foreach (var binding in bindings)
        {
            if (scope.StartsWith(binding.Key, StringComparison.Ordinal))
            {
                return binding.Value;
            }
        }

        return null;
    }
}
