using SharedKernel.Processes;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests the file logging option with a real direct child and an isolated scripted engine.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class IsolatedPodmanFileLoggingTests
{
    [Fact]
    public async Task Direct_child_streams_can_exceed_console_limits_and_preserve_nonzero_exit_status()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var context = new IsolatedPodmanTestContext();
        using var logs = new TemporaryDirectory();
        var awk = PathExecutableResolver.Default.Resolve("awk");
        const int blockCount = 2_048;
        const int blockSize = 4_097;
        const string program = """
            BEGIN {
                for (i = 0; i < 2048; i++) {
                    printf "%04097d", 1
                    printf "%04097d", 2 > "/dev/stderr"
                }
                printf "%c%d", 0, 3
                printf "%c%d", 0, 4 > "/dev/stderr"
                exit 17
            }
            """;

        // Act
        var result = await context.RunCommand(
            ["--log-directory", logs.Path, "--max-log-bytes", "9000000"], [awk, program], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(17, result.Error);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBeEmpty();
        var streams = new[] { (File: "stdout.log", Digit: '1', Suffix: '3'), (File: "stderr.log", Digit: '2', Suffix: '4') };
        foreach (var (file, digit, suffix) in streams)
        {
            using var stream = File.OpenRead(Path.Combine(logs.Path, file));
            stream.Length.ShouldBe(blockCount * blockSize + 2L);
            var block = new byte[blockSize];
            for (var index = 0; index < blockCount; index++)
            {
                stream.ReadExactly(block);
                block.AsSpan(0, blockSize - 1).IndexOfAnyExcept((byte)'0').ShouldBe(-1);
                block[^1].ShouldBe((byte)digit);
            }
            stream.ReadByte().ShouldBe(0);
            stream.ReadByte().ShouldBe(suffix);
        }
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("stdout.log")]
    [InlineData("stderr.log")]
    public async Task Existing_destination_is_preserved_before_the_engine_is_probed(string fileName)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        using var logs = new TemporaryDirectory();
        var existing = Path.Combine(logs.Path, fileName);
        await File.WriteAllTextAsync(existing, "existing output", TestContext.Current.CancellationToken);

        // Act
        var result = await context.RunCommand(
            ["--log-directory", logs.Path, "--max-log-bytes", "1024"], [context.Executable], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(fileName, StringComparison.Ordinal);
        var retained = await File.ReadAllTextAsync(existing, TestContext.Current.CancellationToken);
        retained.ShouldBe("existing output");
        context.Operations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, "stdout.log")]
    [InlineData(true, "stderr.log")]
    public async Task Overrun_retains_the_exact_log_prefix_and_cleans_the_verified_engine(bool standardError, string fileName)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var context = new IsolatedPodmanTestContext();
        using var logs = new TemporaryDirectory();
        var awk = PathExecutableResolver.Default.Resolve("awk");
        var program = standardError
            ? """BEGIN { printf "%04097d", 1 > "/dev/stderr"; }"""
            : """BEGIN { printf "%04097d", 1; }""";

        // Act
        var result = await context.RunCommand(
            ["--log-directory", logs.Path, "--max-log-bytes", "4096"], [awk, program], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("more output than the limit allows", StringComparison.Ordinal);
        var prefix = await File.ReadAllTextAsync(Path.Combine(logs.Path, fileName), TestContext.Current.CancellationToken);
        prefix.ShouldBe(new string('0', 4096));
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
    }

    [Fact]
    public async Task Timeout_retains_both_logs_and_cleans_the_verified_engine()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var context = new IsolatedPodmanTestContext();
        using var logs = new TemporaryDirectory();
        var awk = PathExecutableResolver.Default.Resolve("awk");
        const string program = """
            BEGIN {
                printf "ready-on-out"
                printf "ready-on-error" > "/dev/stderr"
                fflush()
                while (1) {}
            }
            """;

        // Act
        var ct = TestContext.Current.CancellationToken;
        var result = await context.RunCommand(
            ["--log-directory", logs.Path, "--max-log-bytes", "1024", "--timeout-seconds", "1"], [awk, program], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("did not finish within 1 seconds", StringComparison.Ordinal);
        var output = await File.ReadAllTextAsync(Path.Combine(logs.Path, "stdout.log"), ct);
        var error = await File.ReadAllTextAsync(Path.Combine(logs.Path, "stderr.log"), ct);
        output.ShouldBe("ready-on-out");
        error.ShouldBe("ready-on-error");
        Directory.Exists(context.Roots.ShouldHaveSingleItem()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("--log-directory", ".", false, "--log-directory and --max-log-bytes must be supplied together")]
    [InlineData("--max-log-bytes", "1234", false, "--log-directory and --max-log-bytes must be supplied together")]
    [InlineData("--max-log-bytes", "0", true, "--max-log-bytes must be a positive whole number")]
    public async Task Invalid_file_options_fail_before_probing_the_engine(string option, string value, bool addDirectory, string diagnostic)
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        string[] options = addDirectory ? ["--log-directory", context.ImageStore, option, value] : [option, value];

        // Act
        var result = await context.RunCommand(options, [context.Executable], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(diagnostic, StringComparison.Ordinal);
        context.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_log_directory_is_rejected_before_the_engine_is_probed()
    {
        // Arrange
        using var context = new IsolatedPodmanTestContext();
        var path = Path.Combine(context.ImageStore, "logs-do-not-exist");

        // Act
        var result = await context.RunCommand(
            ["--log-directory", path, "--max-log-bytes", "1024"], [context.Executable], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("log directory must be an existing absolute directory", StringComparison.Ordinal);
        Directory.Exists(path).ShouldBeFalse();
        context.Operations.ShouldBeEmpty();
    }
}
