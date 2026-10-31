using Npgsql;

namespace PandaBear.EventSourcing.Npgsql;

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
    /// <exception cref="ArgumentException">When the schema of <paramref name="options"/> is not a valid unquoted PostgreSQL identifier.</exception>
    public static string SchemaName(PostgreSqlEventSourcingOptions? options)
    {
        var schema = options?.Schema ?? new PostgreSqlEventSourcingOptions().Schema;
        if (!IsValidIdentifier(schema))
        {
            throw new ArgumentException("PostgreSQL schema must be a valid unquoted identifier.", nameof(options));
        }

        return schema;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsIdentifierPart(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(char value) => value is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsIdentifierPart(char value) => IsIdentifierStart(value) || value is >= '0' and <= '9';
}
