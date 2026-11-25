using System.Text;

namespace SharedKernel.ConfigurationFiles.Tests;

/// <summary>Tests reading strict JSON configuration files.</summary>
public sealed class JsonConfigurationFileTests
{
    private const string RelativePath = ".config/sample.json";

    [Fact]
    public async Task A_valid_file_is_read()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        await SampleFile.Write(root, """{"version":1,"name":"a"}""");

        // Act
        var configuration = await JsonConfigurationFile.Read(root.Path, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration, TestContext.Current.CancellationToken);

        // Assert
        configuration.ShouldBe(new SampleConfiguration(1, "a"));
    }

    [Fact]
    public async Task A_missing_file_is_an_error_unless_it_is_optional()
    {
        // Arrange
        using var root = new TemporaryDirectory();

        // Act
        Func<Task> read = () => JsonConfigurationFile.Read(root.Path, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration, TestContext.Current.CancellationToken);
        var optional = await JsonConfigurationFile.ReadOptional(root.Path, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration, TestContext.Current.CancellationToken);

        // Assert
        (await read.ShouldThrow<InvalidOperationException>()).Message.ShouldBe(".config/sample.json does not exist.");
        optional.ShouldBeNull();
    }

    [Fact]
    public async Task A_file_over_one_mebibyte_is_refused()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        await SampleFile.Write(root, """{"version":1,"name":"a"}""" + new string(' ', JsonConfigurationFile.MaxBytes));

        // Act
        Func<Task> read = () => JsonConfigurationFile.Read(root.Path, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration, TestContext.Current.CancellationToken);

        // Assert
        (await read.ShouldThrow<InvalidOperationException>()).Message.ShouldBe(".config/sample.json is larger than 1 MiB.");
    }

    [Fact]
    public async Task A_symbolic_link_in_the_path_is_refused()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var root = new TemporaryDirectory();
        using var elsewhere = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(root.Path, ".config"), elsewhere.Path);
        await File.WriteAllTextAsync(Path.Combine(elsewhere.Path, "sample.json"), """{"version":1,"name":"a"}""", TestContext.Current.CancellationToken);

        // Act
        Func<Task> read = () => JsonConfigurationFile.Read(root.Path, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration, TestContext.Current.CancellationToken);

        // Assert
        (await read.ShouldThrow<InvalidOperationException>()).Message.ShouldBe(".config/sample.json: Symbolic links are not followed in repository paths.");
    }

    [Theory]
    [InlineData("""{"version":1,"name":"a","extra":1}""", "$.extra", 1)]
    [InlineData("""{"version":1,"name":"a","name":"b"}""", "$.name", 1)]
    [InlineData("{\n\"version\":1,\n\"name\":2}", "$.name", 3)]
    [InlineData("""{"version":1}""", "$", 1)]
    public void Invalid_content_names_the_JSON_path_and_line(string json, string path, int line)
    {
        // Act
        Action parse = () => JsonConfigurationFile.Parse(Encoding.UTF8.GetBytes(json), RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration);

        // Assert
        parse.ShouldThrow<InvalidOperationException>().Message.ShouldBe(
            $".config/sample.json is not valid at {path} (line {line}). Check for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax.");
    }

    [Fact]
    public void A_JSON_null_is_refused()
    {
        // Act
        Action parse = () => JsonConfigurationFile.Parse("null"u8, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration);

        // Assert
        parse.ShouldThrow<InvalidOperationException>().Message.ShouldBe(".config/sample.json must contain a JSON object.");
    }

    [Fact]
    public void A_utf8_byte_order_mark_is_ignored()
    {
        // Arrange
        byte[] content = [0xEF, 0xBB, 0xBF, .. """{"version":1,"name":"a"}"""u8];

        // Act
        var configuration = JsonConfigurationFile.Parse(content, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration);

        // Assert
        configuration.ShouldBe(new SampleConfiguration(1, "a"));
    }

    [Fact]
    public async Task Invalid_arguments_are_rejected()
    {
        // Act
        Func<Task> withoutRoot = () => JsonConfigurationFile.Read(null!, RelativePath, SampleConfigurationJsonContext.Default.SampleConfiguration, TestContext.Current.CancellationToken);
        Func<Task> withoutType = () => JsonConfigurationFile.Read<SampleConfiguration>("/", RelativePath, null!, TestContext.Current.CancellationToken);
        Action withoutDescription = () => JsonConfigurationFile.Parse("{}"u8, " ", SampleConfigurationJsonContext.Default.SampleConfiguration);

        // Assert
        await withoutRoot.ShouldThrow<ArgumentNullException>();
        await withoutType.ShouldThrow<ArgumentNullException>();
        withoutDescription.ShouldThrow<ArgumentException>();
    }
}
