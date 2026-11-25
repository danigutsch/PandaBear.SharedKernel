namespace SharedKernel.Secrets.Tests;

/// <summary>Tests the editor command that sops edit starts.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class EditorCommandTests
{
    [Fact]
    public void A_command_name_resolves_through_the_resolver_and_keeps_its_arguments()
    {
        // Arrange
        var resolver = new ExecutableMap(new Dictionary<string, string> { ["code"] = "/usr/bin/code" });

        // Act
        var editor = EditorCommand.Create("code  --wait\t--new-window", resolver).ShouldBeSuccess();

        // Assert
        editor.Executable.ShouldBe("/usr/bin/code");
        editor.Arguments.ShouldBe(["--wait", "--new-window"]);
        editor.ToShellWords().ShouldBe("'/usr/bin/code' '--wait' '--new-window'");
    }

    [Fact]
    public void An_absolute_editor_path_is_used_as_it_is_when_the_file_exists()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "my editor");
        File.WriteAllText(path, string.Empty);

        // Act
        var editor = EditorCommand.Create(path, new ExecutableMap(new Dictionary<string, string>())).ShouldBeSuccess();

        // Assert
        editor.Executable.ShouldBe(path);
        editor.Arguments.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("vim")]
    [InlineData("./vim")]
    [InlineData("vim 'file name'")]
    [InlineData("vim \"x\"")]
    [InlineData("vim\u001b[31m")]
    [InlineData("/no/such/editor")]
    public void An_editor_that_is_missing_unresolvable_relative_or_quoted_is_refused(string? commandLine)
    {
        // Act
        var result = EditorCommand.Create(commandLine, new ExecutableMap(new Dictionary<string, string>()));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void The_text_never_reaches_the_refusal_message()
    {
        // Act
        var result = EditorCommand.Create("not-installed-editor --flag", new ExecutableMap(new Dictionary<string, string>()));

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("not-installed-editor", StringComparison.Ordinal);
    }

    [Fact]
    public void An_editor_whose_path_holds_a_quote_is_refused_because_sops_could_not_split_it_back()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var quoted = Directory.CreateDirectory(Path.Combine(directory.Path, "o'brien")).FullName;
        var path = Path.Combine(quoted, "vim");
        File.WriteAllText(path, string.Empty);
        var resolver = new ExecutableMap(new Dictionary<string, string> { ["vim"] = path });

        // Act
        var absolute = EditorCommand.Create(path, resolver);
        var withArguments = EditorCommand.Create(path + " -n", resolver);
        var resolved = EditorCommand.Create("vim -n", resolver);

        // Assert
        absolute.IsFailure.ShouldBeTrue();
        withArguments.IsFailure.ShouldBeTrue();
        resolved.IsFailure.ShouldBeTrue();
    }
}
