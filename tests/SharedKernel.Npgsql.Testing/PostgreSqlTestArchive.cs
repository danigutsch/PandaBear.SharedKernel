namespace SharedKernel.Npgsql.Testing;

/// <summary>Identifies a private custom-format dump captured from a leased PostgreSQL test database.</summary>
public sealed class PostgreSqlTestArchive
{
    /// <summary>Creates a receipt for a captured archive in its caller-owned workspace.</summary>
    internal PostgreSqlTestArchive(string path, string sha256, long length, string tableOfContents, string sourceDatabaseName)
    {
        Path = path;
        Sha256 = sha256;
        Length = length;
        TableOfContents = tableOfContents;
        SourceDatabaseName = sourceDatabaseName;
    }

    /// <summary>Gets the archive file owned by the caller's test workspace.</summary>
    public string Path { get; }

    /// <summary>Gets the SHA-256 of the captured binary file.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the archive size in bytes.</summary>
    public long Length { get; }

    /// <summary>Gets the bounded text returned by the native archive listing command.</summary>
    public string TableOfContents { get; }

    /// <summary>Gets the generated source database name used to guard restore.</summary>
    internal string SourceDatabaseName { get; }
}
