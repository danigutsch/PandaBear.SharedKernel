using System.Data.Common;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a SQLite database file, <c>{name}.db</c>, in a data directory.
/// </summary>
/// <param name="name">The name of the resource; the database file is <c>{name}.db</c>.</param>
/// <param name="dataDirectory">The absolute path of the directory that holds the database file.</param>
public sealed class SqliteResource([ResourceName] string name, string dataDirectory) : Resource(name), IResourceWithConnectionString
{
    /// <summary>
    /// Gets the absolute path of the directory that holds the database file. Other files that belong with the database, such as
    /// uploaded media, can live in it too and share its lifetime: deleted on stop when ephemeral, kept when bind-mounted.
    /// </summary>
    /// <remarks>
    /// <c>WithDataBindMount</c> changes it, so read it lazily, for example inside a <c>WithEnvironment</c> callback, when the read can
    /// happen before that call.
    /// </remarks>
    public string DataDirectory { get; internal set; } = dataDirectory;

    /// <summary>
    /// Gets the absolute path of the database file, <c>{DataDirectory}/{name}.db</c>.
    /// </summary>
    public string DatabasePath => Path.Combine(DataDirectory, Name + ".db");

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
