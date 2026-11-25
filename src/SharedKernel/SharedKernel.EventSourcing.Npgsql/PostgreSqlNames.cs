using Npgsql;
using SharedKernel.InputNormalization;

namespace SharedKernel.EventSourcing.Npgsql;

/// <summary>Resolves and quotes the PostgreSQL schema name used by event sourcing.</summary>
internal static class PostgreSqlNames
{
    /// <summary>Returns the configured schema name quoted for safe use in SQL text.</summary>
    public static string Schema(PostgreSqlEventSourcingOptions? options)
    {
        var schema = SchemaName(options);
        using var commandBuilder = new NpgsqlCommandBuilder();
        return commandBuilder.QuoteIdentifier(schema);
    }

    /// <summary>Returns the configured schema name, or the default when no options are given, after checking it is a valid identifier.</summary>
    /// <exception cref="ArgumentException">Thrown when the schema of <paramref name="options"/> is not a valid unquoted PostgreSQL identifier.</exception>
    public static string SchemaName(PostgreSqlEventSourcingOptions? options)
    {
        var schema = options?.Schema ?? new PostgreSqlEventSourcingOptions().Schema;
        if (!PostgreSqlIdentifier.IsUnquoted(schema))
        {
            throw new ArgumentException("PostgreSQL schema must be a valid unquoted identifier.", nameof(options));
        }

        return schema;
    }
}
