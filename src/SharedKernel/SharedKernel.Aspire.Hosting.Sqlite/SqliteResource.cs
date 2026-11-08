using System.Data.Common;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a SQLite database file.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="databasePath">The absolute path of the database file.</param>
public sealed class SqliteResource([ResourceName] string name, string databasePath) : Resource(name), IResourceWithConnectionString
{
    /// <summary>
    /// Gets the absolute path of the database file.
    /// </summary>
    public string DatabasePath { get; } = databasePath;

    /// <summary>
    /// Gets the connection string expression for the database, <c>Data Source=&lt;path&gt;</c>.
    /// </summary>
    /// <remarks>
    /// A path that needs quoting, for example one containing a semicolon, is quoted by <see cref="DbConnectionStringBuilder"/>.
    /// </remarks>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{BuildConnectionString(DatabasePath)}");

    /// <inheritdoc />
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("DataSource", ReferenceExpression.Create($"{DatabasePath}"));
    }

    private static string BuildConnectionString(string path) =>
        new DbConnectionStringBuilder { ["Data Source"] = path }.ConnectionString;
}
