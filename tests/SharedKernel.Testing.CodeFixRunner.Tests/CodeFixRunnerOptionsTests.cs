
namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunnerOptionsTests
{
    [Fact]
    public void Parse_uses_default_diagnostic_for_single_target_argument()
    {
        var options = CodeFixRunnerOptions.Parse(["sample.csproj"]);

        var parsedOptions = TestAssert.NotNull(options);
        TestAssert.Equal(Path.GetFullPath("sample.csproj"), parsedOptions.TargetPath);
        TestAssert.Equal("SKTEST004", parsedOptions.DiagnosticId);
    }

    [Fact]
    public void Parse_uses_requested_diagnostic_when_option_is_provided()
    {
        var options = CodeFixRunnerOptions.Parse(["--diagnostic", "SKTEST006", "sample.csproj"]);

        var parsedOptions = TestAssert.NotNull(options);
        TestAssert.Equal(Path.GetFullPath("sample.csproj"), parsedOptions.TargetPath);
        TestAssert.Equal("SKTEST006", parsedOptions.DiagnosticId);
    }

    [Theory]
    [InlineData()]
    [InlineData("--diagnostic")]
    [InlineData("--unknown", "SKTEST006", "sample.csproj")]
    [InlineData("sample.csproj", "extra")]
    public void Parse_returns_null_for_invalid_arguments(params string[] args)
    {
        var options = CodeFixRunnerOptions.Parse(args);

        TestAssert.Null(options);
    }
}
