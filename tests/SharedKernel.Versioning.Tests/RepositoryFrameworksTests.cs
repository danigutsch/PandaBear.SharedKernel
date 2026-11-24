namespace PandaBear.Versioning.Tests;

/// <summary>Tests reading the target frameworks of a repository from its root Directory.Build.props.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class RepositoryFrameworksTests
{
    [Fact]
    public static void Reads_a_single_framework_through_a_property()
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, "<A>net11.0</A><TargetFramework>$(A)</TargetFramework>");

        // Act
        var frameworks = RepositoryFrameworks.Read(temporary.Path);

        // Assert
        frameworks.ShouldBe(["net11.0"]);
    }

    [Fact]
    public static void Reads_a_framework_list()
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, "<TargetFrameworks>net11.0;net12.0</TargetFrameworks><TargetFramework>net10.0</TargetFramework>");

        // Act
        var frameworks = RepositoryFrameworks.Read(temporary.Path);

        // Assert
        frameworks.ShouldBe(["net11.0", "net12.0"]);
    }

    [Fact]
    public static void An_empty_framework_list_falls_back_to_the_single_framework()
    {
        // Arrange
        // MSBuild builds TargetFramework when TargetFrameworks is empty, as a project that clears the list does.
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, "<TargetFrameworks></TargetFrameworks><TargetFramework>net11.0</TargetFramework>");

        // Act
        var frameworks = RepositoryFrameworks.Read(temporary.Path);

        // Assert
        frameworks.ShouldBe(["net11.0"]);
    }

    [Theory]
    [InlineData("<A>$(A)</A><TargetFramework>$(A)</TargetFramework>")]
    [InlineData("<A>$(B)</A><B>$(A)</B><TargetFramework>$(A)</TargetFramework>")]
    public static void A_property_that_refers_to_itself_is_rejected(string properties)
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, properties);

        // Act
        Action act = () => RepositoryFrameworks.Read(temporary.Path);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("refers to itself", StringComparison.Ordinal);
    }

    [Fact]
    public static void A_missing_file_names_the_file()
    {
        // Arrange
        using var temporary = new TemporaryDirectory();

        // Act
        var action = () => RepositoryFrameworks.Read(temporary.Path);

        // Assert
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(Path.Combine(temporary.Path, "Directory.Build.props"), StringComparison.Ordinal);
    }

    [Fact]
    public static void A_missing_property_names_the_file()
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, "<Other>x</Other>");

        // Act
        var action = () => RepositoryFrameworks.Read(temporary.Path);

        // Assert
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(Path.Combine(temporary.Path, "Directory.Build.props"), StringComparison.Ordinal);
    }

    [Fact]
    public static void A_conditional_property_is_rejected()
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, "<TargetFramework Condition=\"'$(X)' == ''\">net11.0</TargetFramework>");

        // Act
        var action = () => RepositoryFrameworks.Read(temporary.Path);

        // Assert
        var message = action.ShouldThrow<InvalidOperationException>().Message;
        message.ShouldContain(Path.Combine(temporary.Path, "Directory.Build.props"), StringComparison.Ordinal);
        message.ShouldContain("conditional", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public static void A_conditional_property_group_is_rejected()
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(temporary.Path, "Directory.Build.props"),
            "<Project><PropertyGroup Condition=\"'$(X)' == ''\"><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");

        // Act
        var action = () => RepositoryFrameworks.Read(temporary.Path);

        // Assert
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain("conditional", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("net11")]
    [InlineData("$(Missing)")]
    [InlineData("net11.0-windows")]
    public static void A_value_that_is_not_a_moniker_is_rejected(string value)
    {
        // Arrange
        using var temporary = new TemporaryDirectory();
        FrameworkPropsFile.Write(temporary.Path, "<TargetFramework>" + value + "</TargetFramework>");

        // Act
        var action = () => RepositoryFrameworks.Read(temporary.Path);

        // Assert
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain(Path.Combine(temporary.Path, "Directory.Build.props"), StringComparison.Ordinal);
    }
}
