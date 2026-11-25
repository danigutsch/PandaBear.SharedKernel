namespace PandaBear.Secrets.Tests;

/// <summary>Tests that a request cannot carry key material outside standard input.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class ToolRequestTests
{
    private const string Key = FakeKeys.Secret;

    [Theory]
    [InlineData(Key)]
    [InlineData("prefix " + Key)]
    [InlineData(FakeKeys.LowercaseSecret)]
    [InlineData("AGE-PLUGIN-YUBIKEY-1QQQQQQ")]
    public void Key_material_in_an_argument_is_refused_without_echoing_it(string argument)
    {
        // Act
        Action act = () => _ = new ToolRequest("/usr/bin/age", ["--decrypt", argument]);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldNotContain("QQQQ", StringComparison.Ordinal);
    }

    [Fact]
    public void Key_material_in_an_environment_value_is_refused()
    {
        // Act
        Action act = () => _ = new ToolRequest("/usr/bin/sops", [], new Dictionary<string, string> { ["SOPS_AGE_KEY"] = Key });

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Key_material_is_allowed_on_standard_input()
    {
        // Act
        var request = new ToolRequest("/usr/bin/age", [], standardInput: System.Text.Encoding.UTF8.GetBytes(Key));

        // Assert
        request.StandardInput.Length.ShouldBe(Key.Length);
    }

    [Fact]
    public void A_relative_executable_and_null_arguments_are_refused()
    {
        // Act
        Action relative = () => _ = new ToolRequest("age", []);
        Action noExecutable = () => _ = new ToolRequest(null!, []);
        Action noArguments = () => _ = new ToolRequest("/usr/bin/age", null!);

        // Assert
        relative.ShouldThrow<ArgumentException>();
        noExecutable.ShouldThrow<ArgumentNullException>();
        noArguments.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void The_request_keeps_copies_of_its_arguments_and_environment()
    {
        // Arrange
        List<string> arguments = ["a"];
        Dictionary<string, string> environment = new()
        {
            ["K"] = "v"
        };

        // Act
        var request = new ToolRequest("/usr/bin/age", arguments, environment);
        arguments.Add("b");
        environment["L"] = "w";

        // Assert
        request.Arguments.ShouldBe(["a"]);
        request.Environment.Count.ShouldBe(1);
        request.Executable.ShouldBe("/usr/bin/age");
    }

    [Fact]
    public void A_result_rejects_null_parts()
    {
        // Act
        Action noOutput = () => _ = new ToolResult(0, null!);
        using var output = SecretBytes.CopyOf([]);
        Action noError = () => _ = new ToolResult(0, output, null!);

        // Assert
        noOutput.ShouldThrow<ArgumentNullException>();
        noError.ShouldThrow<ArgumentNullException>();
    }
}
