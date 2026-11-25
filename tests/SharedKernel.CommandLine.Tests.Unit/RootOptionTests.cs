namespace SharedKernel.CommandLine.Tests;

/// <summary>Tests the shared <c>--root</c> option.</summary>
public sealed class RootOptionTests
{
    [Fact]
    public void Without_the_option_the_root_is_the_working_directory()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        var result = CommandParsing.Parse([option]);

        // Assert
        result.Errors.ShouldBeEmpty();
        option.GetDirectory(result).ShouldBe(Path.GetFullPath(workingDirectory.Path));
    }

    [Fact]
    public void A_relative_root_resolves_against_the_working_directory()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        var result = CommandParsing.Parse([option], "--root", Path.Combine("..", "app"));

        // Assert
        result.Errors.ShouldBeEmpty();
        option.GetDirectory(result).ShouldBe(Path.GetFullPath(Path.Combine(workingDirectory.Path, "..", "app")));
    }

    [Fact]
    public void An_absolute_root_is_kept()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        using var other = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        var result = CommandParsing.Parse([option], "--root", other.Path);

        // Assert
        option.GetDirectory(result).ShouldBe(Path.GetFullPath(other.Path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void An_empty_or_white_space_root_is_a_parse_error_instead_of_the_working_directory(string root)
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        var result = CommandParsing.Parse([option], "--root", root);

        // Assert
        CommandParsing.Errors(result).ShouldBe([RootOption.EmptyMessage]);
    }

    [Fact]
    public void A_root_with_a_null_character_is_a_parse_error()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        var result = CommandParsing.Parse([option], "--root", "a\0b");

        // Assert
        CommandParsing.Errors(result).ShouldBe([RootOption.NullCharacterMessage]);
    }

    [Fact]
    public void A_root_option_without_a_value_is_a_parse_error()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        var result = CommandParsing.Parse([option], "--root");

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void An_existing_root_is_returned_as_an_existing_directory()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(workingDirectory.Path, "app"));
        var option = new RootOption(workingDirectory.Path);
        var result = CommandParsing.Parse([option], "--root", "app");

        // Act
        var directory = option.GetExistingDirectory(result);

        // Assert
        directory.ShouldBe(Path.Combine(Path.GetFullPath(workingDirectory.Path), "app"));
    }

    [Fact]
    public void A_missing_root_directory_fails_with_one_message_that_names_it()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);
        var result = CommandParsing.Parse([option], "--root", "missing");
        var expected = Path.Combine(Path.GetFullPath(workingDirectory.Path), "missing");

        // Act
        Action act = () => _ = option.GetExistingDirectory(result);
        var exception = act.ShouldThrow<DirectoryNotFoundException>();

        // Assert
        exception.Message.ShouldBe("--root is not an existing directory: " + expected);
    }

    [Fact]
    public void A_root_that_is_a_file_is_not_an_existing_directory()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        File.WriteAllText(Path.Combine(workingDirectory.Path, "file"), string.Empty);
        var option = new RootOption(workingDirectory.Path);
        var result = CommandParsing.Parse([option], "--root", "file");

        // Act
        Action act = () => _ = option.GetExistingDirectory(result);
        var exception = act.ShouldThrow<DirectoryNotFoundException>();

        // Assert
        exception.Message.ShouldStartWith("--root is not an existing directory: ", StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_directory_message_escapes_control_characters()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);
        var result = CommandParsing.Parse([option], "--root", "a\u001bb");

        // Act
        Action act = () => _ = option.GetExistingDirectory(result);
        var exception = act.ShouldThrow<DirectoryNotFoundException>();

        // Assert
        exception.Message.ShouldEndWith("a\\u001Bb", StringComparison.Ordinal);
    }

    [Fact]
    public void The_option_is_named_root_and_keeps_the_description()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();

        // Act
        var option = new RootOption(workingDirectory.Path, "Holds the configuration.");

        // Assert
        option.Name.ShouldBe("--root");
        option.HelpName.ShouldBe("path");
        option.Description.ShouldBe("Holds the configuration.");
        new RootOption(workingDirectory.Path).Description.ShouldBe(RootOption.DefaultDescription);
    }

    [Fact]
    public void A_relative_working_directory_is_refused()
    {
        // Act
        Action act = () => _ = new RootOption("relative");
        var exception = act.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("workingDirectory");
    }

    [Fact]
    public void A_null_parse_result_is_refused()
    {
        // Arrange
        using var workingDirectory = new Testing.TemporaryDirectory();
        var option = new RootOption(workingDirectory.Path);

        // Act
        Action act = () => _ = option.GetDirectory(null!);
        var exception = act.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("parseResult");
    }
}
