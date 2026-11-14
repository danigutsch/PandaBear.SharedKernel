using System.Data.Common;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Tests giving a resource read-only access to a SQLite database file that another resource owns.</summary>
public sealed class SqliteReadOnlyReferenceTests
{
    /// <summary>Verifies that the consumer receives the database path opened in read-only mode.</summary>
    [Fact]
    public async Task WithReadOnlyReference_injects_a_read_only_connection_string()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "db.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);
        var consumer = builder.AddContainer("public", "example/image").WithReadOnlyReference(sqlite, connectionName: "Default");

        // Act
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        variables["ConnectionStrings__Default"].ShouldBe($"Data Source={path};Mode=ReadOnly");
    }

    /// <summary>Verifies that the connection is named after the database resource when no name is given.</summary>
    [Fact]
    public async Task WithReadOnlyReference_uses_the_resource_name_by_default()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("site-db").WithDataBindMount(temp.Path);
        var consumer = builder.AddContainer("public", "example/image").WithReadOnlyReference(sqlite);

        // Act
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        variables.Keys.ShouldContain("ConnectionStrings__site-db");
    }

    /// <summary>Verifies that a path with a semicolon stays one data source and the mode cannot be overridden through the path.</summary>
    [Fact]
    public async Task WithReadOnlyReference_quotes_a_path_with_a_semicolon()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var folder = Path.Combine(temp.Path, "a;Mode=ReadWriteCreate");
        var path = Path.Combine(folder, "db.db");
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(folder);
        var consumer = builder.AddContainer("public", "example/image").WithReadOnlyReference(sqlite, connectionName: "Default");

        // Act
        var variables = await EnvironmentVariables.Of(consumer.Resource);

        // Assert
        var parsed = new DbConnectionStringBuilder { ConnectionString = variables["ConnectionStrings__Default"] };
        parsed["Data Source"].ShouldBe(path);
        parsed["Mode"].ShouldBe("ReadOnly");
    }

    /// <summary>Verifies that the dashboard shows the consumer's reference to the database.</summary>
    [Fact]
    public void WithReadOnlyReference_records_a_reference_relationship()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);

        // Act
        var consumer = builder.AddContainer("public", "example/image").WithReadOnlyReference(sqlite);

        // Assert
        var relationship = consumer.Resource.Annotations.OfType<ResourceRelationshipAnnotation>().ShouldHaveSingleItem();
        relationship.Resource.ShouldBeSameAs(sqlite.Resource);
        relationship.Type.ShouldBe("Reference");
    }

    /// <summary>Verifies the argument validation of <c>WithReadOnlyReference</c>.</summary>
    [Fact]
    public void WithReadOnlyReference_validates_its_arguments()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db").WithDataBindMount(temp.Path);
        var consumer = builder.AddContainer("public", "example/image");

        // Act
        var nullBuilder = () => SqliteResourceExtensions.WithReadOnlyReference<ContainerResource>(null!, sqlite);
        var nullDatabase = () => consumer.WithReadOnlyReference(null!);
        var blankName = () => consumer.WithReadOnlyReference(sqlite, connectionName: " ");

        // Assert
        nullBuilder.ShouldThrow<ArgumentNullException>();
        nullDatabase.ShouldThrow<ArgumentNullException>();
        blankName.ShouldThrow<ArgumentException>();
    }
}
