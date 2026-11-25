using System.Text;

namespace SharedKernel.Repositories.Tests;

/// <summary>Tests the boundary between evaluated NuGet proposals and tracked central props.</summary>
public sealed class NuGetTrialPropsTests
{
    [Theory]
    [InlineData("Package")]
    [InlineData("@1.0.0")]
    [InlineData("Package@")]
    [InlineData("-Package@1.0.0")]
    [InlineData("Package@@2.0.0")]
    [InlineData("Package@[2.0.0,3.0.0)")]
    public void Parse_updates_requires_an_unambiguous_package_and_exact_version(string specification)
    {
        // Act
        Action parse = () => NuGetTrialProps.ParseUpdates([specification]);

        // Assert
        parse.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Parse_updates_disallows_repeated_IDs_with_different_casing()
    {
        // Act
        Action parse = () => NuGetTrialProps.ParseUpdates(["Package@1.0.0", "package@2.0.0"]);

        // Assert
        parse.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task Apply_changes_only_the_selected_declarations_and_preserves_the_byte_order_mark()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "Versions.props");
        var ct = TestContext.Current.CancellationToken;
        const string original = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Package" Version = "1.0.0" />
                <!-- leave this --> <PackageVersion Include='Pin' Version='2.0.0'><Unused /></PackageVersion>
                <PackageVersion Include="Other" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(file, original, Encoding.UTF8, ct);

        // Act
        await NuGetTrialProps.Apply(file, "Pin", NuGetTrialProps.ParseUpdates(["Package@2.0.0"]), ct);

        // Assert
        var bytes = await File.ReadAllBytesAsync(file, ct);
        var expected = original.Replace("Version = \"1.0.0\"", "Version = \"2.0.0\"", StringComparison.Ordinal)
            .Replace("<PackageVersion Include='Pin' Version='2.0.0'><Unused /></PackageVersion>", string.Empty, StringComparison.Ordinal);
        bytes.ShouldBe([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(expected)]);
    }

    [Theory]
    [InlineData("<PackageVersion Include='Package'><Version>1.0.0</Version></PackageVersion>")]
    [InlineData("<PackageVersion Include='Package' Version='1.0.0'/><PackageVersion Update='Package' Version='2.0.0'/>")]
    public async Task Apply_rejects_an_ambiguous_or_unsupported_declaration_without_editing_it(string declaration)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "Versions.props");
        var ct = TestContext.Current.CancellationToken;
        var original = $"<Project><ItemGroup>{declaration}</ItemGroup></Project>";
        await File.WriteAllTextAsync(file, original, ct);

        // Act
        Func<Task> apply = () => NuGetTrialProps.Apply(file, null, NuGetTrialProps.ParseUpdates(["Package@2.0.0"]), ct);

        // Assert
        await apply.ShouldThrow<InvalidOperationException>();
        var after = await File.ReadAllTextAsync(file, ct);
        after.ShouldBe(original);
    }

    [Fact]
    public async Task Preserve_formatting_rejects_changes_to_a_package_condition()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "Versions.props");
        var ct = TestContext.Current.CancellationToken;
        const string original = "<Project><ItemGroup><PackageVersion Include='Package' Version='1.0.0'/></ItemGroup></Project>";
        var sdkEdited = original.Replace("Version='1.0.0'", "Version='2.0.0' Condition='false'", StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, sdkEdited, ct);

        // Act
        Func<Task> preserve = () => NuGetTrialProps.PreserveFormatting(file, Encoding.UTF8.GetBytes(original), ct);

        // Assert
        await preserve.ShouldThrow<InvalidOperationException>();
    }
}
