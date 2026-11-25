namespace PandaBear.Packages.Tests;

/// <summary>Tests reading and validating <c>.config/sharedkernel-packages.json</c>.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackagesConfigurationTests
{
    private static readonly CancellationToken Cancel = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_valid_configuration_gives_the_declared_packages_and_the_age()
    {
        // Arrange
        using var repository = ConfigurationRepository.Create("""{"version":1,"packages":{"cargo":["bat","ripgrep=14.1.0"],"apt":["git"]},"minimumReleaseAgeDays":7}""");

        // Act
        var configuration = await PackagesConfiguration.Read(repository.Path, Cancel);

        // Assert
        configuration.Packages.Managers.ShouldBe([PackageManagerKind.Apt, PackageManagerKind.Cargo]);
        configuration.Packages.For(PackageManagerKind.Cargo).Select(static r => r.ToString()).ShouldBe(["bat", "ripgrep=14.1.0"]);
        configuration.MinimumReleaseAge.ShouldBe(PackageFactory.Age(7));
    }

    [Fact]
    public async Task Without_an_age_the_age_is_null()
    {
        // Arrange
        using var repository = ConfigurationRepository.Create("""{"version":1,"packages":{}}""");

        // Act
        var configuration = await PackagesConfiguration.Read(repository.Path, Cancel);

        // Assert
        configuration.MinimumReleaseAge.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"version":2,"packages":{}}""", "$.version must be 1.")]
    [InlineData("""{"version":1,"packages":{},"minimumReleaseAgeDays":3651}""", "$.minimumReleaseAgeDays must be from 0 to 3650.")]
    [InlineData("""{"version":1,"packages":{"pip":["x"]}}""", "$.packages.pip is not a known package manager")]
    [InlineData("""{"version":1,"packages":{"apt":null}}""", "$.packages.apt must be a list of package names, not null.")]
    [InlineData("""{"version":1,"packages":{"apt":[null]}}""", "$.packages.apt[0] must be a package name, not null.")]
    [InlineData("""{"version":1,"packages":{"apt":["git="]}}""", "$.packages.apt[0] must have a valid version after '='.")]
    [InlineData("""{"version":1,"packages":{"apt":["git"],"brew":["git"]}}""", "$.packages gives a package more than one owner:")]
    public async Task An_invalid_configuration_names_the_file_and_the_JSON_path(string json, string expected)
    {
        // Arrange
        using var repository = ConfigurationRepository.Create(json);
        Func<Task> read = () => PackagesConfiguration.Read(repository.Path, Cancel);

        // Act
        var exception = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(".config/sharedkernel-packages.json: " + expected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_null_root_is_rejected()
    {
        // Arrange
        Func<Task> read = static () => PackagesConfiguration.Read(null!, Cancel);

        // Act and assert
        await read.ShouldThrow<ArgumentNullException>();
    }
}
