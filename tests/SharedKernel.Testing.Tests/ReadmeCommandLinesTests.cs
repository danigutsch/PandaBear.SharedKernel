namespace SharedKernel.Testing.Tests;

/// <summary>Tests finding the documented command lines of one invocation in Markdown.</summary>
public sealed class ReadmeCommandLinesTests
{
    private const string Readme = """
        # Tool

        The command is `dotnet tool`. Run `dotnet tool verify --root .` before a commit, or
        `dotnet tool run tool -- verify` as a local tool.

        ```sh
        dotnet tool init
          dotnet tool verify --root path   # check a repository
        TOKEN=abc DEBUG_LEVEL=2 dotnet tool publish --force
        dotnet toolbox list
        dotnet tool
        tool verify
        ```
        """;

    [Fact]
    public void Find_returns_the_arguments_of_lines_and_inline_code_in_document_order()
    {
        // Act
        var commandLines = ReadmeCommandLines.Find(Readme, "dotnet tool");

        // Assert
        commandLines.ShouldBe(["verify --root .", "run tool -- verify", "init", "verify --root path", "publish --force"]);
    }

    [Fact]
    public void Find_matches_a_longer_invocation_such_as_a_local_tool_run()
    {
        // Act
        var commandLines = ReadmeCommandLines.Find(Readme, "dotnet tool run tool --");

        // Assert
        commandLines.ShouldBe(["verify"]);
    }

    [Fact]
    public void Find_ignores_a_bare_command_name_and_a_word_that_only_starts_with_the_command()
    {
        // Act
        var commandLines = ReadmeCommandLines.Find("tool verify\ndotnet toolbox list\nrun dotnet tool verify later", "dotnet tool");

        // Assert
        commandLines.ShouldBeEmpty();
    }

    [Fact]
    public void Find_reads_lines_that_end_with_a_carriage_return()
    {
        // Act
        var commandLines = ReadmeCommandLines.Find("dotnet tool init\r\ndotnet tool verify\r\n", "dotnet tool");

        // Assert
        commandLines.ShouldBe(["init", "verify"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Find_rejects_a_blank_invocation(string invocation)
    {
        // Arrange
        Func<object?> find = () => ReadmeCommandLines.Find(Readme, invocation);

        // Act
        var exception = find.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("invocation");
    }
}
