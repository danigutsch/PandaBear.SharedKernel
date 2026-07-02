
namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunnerOptionsTests
{
    [Fact]
    public void Parse_uses_default_diagnostic_for_single_target_argument()
    {
        var options = CodeFixRunnerOptions.Parse(["sample.csproj"]);

        var parsedOptions = (options).ShouldNotBeNull();
        (parsedOptions.TargetPath).ShouldBe(Path.GetFullPath("sample.csproj"));
        (parsedOptions.DiagnosticId).ShouldBe("SKTEST004");
    }

    [Fact]
    public void Parse_uses_requested_diagnostic_when_option_is_provided()
    {
        var options = CodeFixRunnerOptions.Parse(["--diagnostic", "SKTEST006", "sample.csproj"]);

        var parsedOptions = (options).ShouldNotBeNull();
        (parsedOptions.TargetPath).ShouldBe(Path.GetFullPath("sample.csproj"));
        (parsedOptions.DiagnosticId).ShouldBe("SKTEST006");
    }

    [Theory]
    [InlineData()]
    [InlineData("--diagnostic")]
    [InlineData("--unknown", "SKTEST006", "sample.csproj")]
    [InlineData("sample.csproj", "extra")]
    public void Parse_returns_null_for_invalid_arguments(params string[] args)
    {
        var options = CodeFixRunnerOptions.Parse(args);

        (options).ShouldBeNull();
    }
}
