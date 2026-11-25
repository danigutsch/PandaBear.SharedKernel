using Aspire.Hosting;

namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>Tests WithDataBindMountFromConfiguration: the database persists in the configured folder, and stays ephemeral without one.</summary>
public sealed class SqliteDataBindMountFromConfigurationTests
{
    [Fact]
    public void A_configured_folder_keeps_the_database_resolved_against_the_app_host_directory()
    {
        // Arrange
        using var temp = new TemporaryDirectory();
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration["AppHost:DataFolder"] = Path.GetRelativePath(builder.AppHostDirectory, temp.Path);

        // Act
        var sqlite = builder.AddSqlite("db").WithDataBindMountFromConfiguration("AppHost:DataFolder");

        // Assert
        sqlite.Resource.DataDirectory.ShouldBe(temp.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Without_a_configured_folder_the_database_stays_ephemeral(string? folder)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration["AppHost:DataFolder"] = folder;
        var sqlite = builder.AddSqlite("db");
        var ephemeral = sqlite.Resource.DataDirectory;

        // Act
        sqlite.WithDataBindMountFromConfiguration("AppHost:DataFolder");

        // Assert
        sqlite.Resource.DataDirectory.ShouldBe(ephemeral);
        Path.GetFileName(ephemeral).ShouldStartWith("aspire-sqlite-db-", StringComparison.Ordinal);
    }

    [Fact]
    public void A_blank_configuration_key_is_refused()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var sqlite = builder.AddSqlite("db");

        // Act
        Action configure = () => sqlite.WithDataBindMountFromConfiguration(" ");

        // Assert
        configure.ShouldThrow<ArgumentException>().ParamName.ShouldBe("configurationKey");
    }
}
