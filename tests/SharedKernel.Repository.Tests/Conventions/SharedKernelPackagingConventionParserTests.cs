namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Guards the YAML path parser that the packaging conventions use.</summary>
public sealed class SharedKernelPackagingConventionParserTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Yaml_path_entries_do_not_depend_on_host_line_endings(string newline)
    {
        // Arrange
        var content = string.Join(newline, "paths:", "  - src/SharedKernel/**", "  - Directory.Build.props");

        // Act
        var entries = SharedKernelPackagingConventionTestFiles.GetYamlPathEntries(content);

        // Assert
        entries.Count.ShouldBe(2);
        entries.ShouldContain("src/SharedKernel/**");
        entries.ShouldContain("Directory.Build.props");
    }
}
