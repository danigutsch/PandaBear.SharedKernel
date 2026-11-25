namespace SharedKernel.PostgreSql;

/// <summary>
/// Checks names that are written into SQL text as PostgreSQL identifiers, such as a configured schema or table name.
/// </summary>
public static class PostgreSqlIdentifier
{
    private const int MaxByteCount = 63;

    /// <summary>
    /// Reports whether <paramref name="value"/> is a PostgreSQL identifier that needs no quotes and that PostgreSQL keeps
    /// whole: an ASCII letter or <c>_</c>, then ASCII letters, digits, or <c>_</c>, at most 63 bytes long. PostgreSQL
    /// truncates a longer name to NAMEDATALEN-1 (63) bytes, so two long names could select the same object. Only ASCII is
    /// accepted, so each character is one byte. The value is not lowercased; PostgreSQL folds an unquoted name to lower
    /// case.
    /// </summary>
    /// <param name="value">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is accepted; otherwise, <see langword="false"/>.</returns>
    public static bool IsUnquoted(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxByteCount || !IsStart(value[0]))
        {
            return false;
        }

        foreach (var character in value.AsSpan(1))
        {
            if (!IsStart(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStart(char value) => value == '_' || char.IsAsciiLetter(value);
}
