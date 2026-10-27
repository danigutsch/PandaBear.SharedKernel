namespace PandaBear.IO.Analyzers.Tests;

/// <summary>
/// Tests that SharedKernelIOAnalyzer reports unbounded stream buffering and hand-rolled copy loops, and accepts explicitly bounded code.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.StreamBufferingCapability)]
public sealed class SharedKernelIOAnalyzerTests
{
    [Fact]
    public void Supported_diagnostics_match_io_descriptor_contract()
    {
        // Arrange
        var analyzer = new SharedKernelIOAnalyzer();

        // Act
        var diagnosticIds = analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id);

        // Assert
        diagnosticIds.ShouldBe([IODiagnosticIds.UnboundedBuffering, IODiagnosticIds.HandRolledCopyLoop]);
    }

    [Fact]
    public async Task Generated_files_are_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                public Task<byte[]> Load(HttpContent content) => content.ReadAsByteArrayAsync();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "Generated.g.cs");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Copy_to_memory_stream_from_form_file_reports_skio001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(IFormFile file)
                {
                    await using var input = file.OpenReadStream();
                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO001");
    }

    [Fact]
    public async Task Copy_to_memory_stream_from_request_body_reports_skio001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public void Accept(HttpRequest request)
                {
                    using var buffer = new MemoryStream();
                    request.Body.CopyTo(buffer);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO001");
    }

    [Fact]
    public async Task Copy_to_memory_stream_with_length_check_against_max_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(IFormFile file)
                {
                    if (file.Length > Max)
                    {
                        throw new InvalidDataException();
                    }

                    await using var input = file.OpenReadStream();
                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Copy_to_memory_stream_from_limited_wrapper_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(IFormFile file)
                {
                    await using var input = new LimitedStream(file.OpenReadStream(), Max);
                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer);
                }

                private sealed class LimitedStream(Stream inner, long max) : Stream
                {
                    public override bool CanRead => true;
                    public override bool CanSeek => false;
                    public override bool CanWrite => false;
                    public override long Length => max;
                    public override long Position { get => 0; set => throw new NotSupportedException(); }
                    public override void Flush() { }
                    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
                    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                    public override void SetLength(long value) => throw new NotSupportedException();
                    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Copy_to_memory_stream_from_unrelated_stream_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Load(string path)
                {
                    await using var input = File.OpenRead(path);
                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Copy_to_file_stream_from_form_file_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(IFormFile file, string path)
                {
                    await using var input = file.OpenReadStream();
                    await using var output = File.Create(path);
                    await input.CopyToAsync(output);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_as_byte_array_reports_skio001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task<byte[]> Load(HttpContent content) => await content.ReadAsByteArrayAsync();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO001");
    }

    [Fact]
    public async Task Read_as_byte_array_with_content_length_check_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task<byte[]> Load(HttpContent content)
                {
                    if (content.Headers.ContentLength > Max)
                    {
                        throw new InvalidDataException();
                    }

                    return await content.ReadAsByteArrayAsync();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_to_end_on_reader_over_form_file_reports_skio001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task<string> Accept(IFormFile file)
                {
                    using var reader = new StreamReader(file.OpenReadStream());
                    return await reader.ReadToEndAsync();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO001");
    }

    [Fact]
    public async Task Read_to_end_on_reader_over_request_body_reports_skio001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public string Accept(HttpRequest request)
                {
                    using var reader = new StreamReader(request.Body);
                    return reader.ReadToEnd();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO001");
    }

    [Fact]
    public async Task Read_to_end_on_reader_over_file_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public string Load(string path)
                {
                    using var reader = new StreamReader(path);
                    return reader.ReadToEnd();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_loop_into_memory_stream_reports_skio002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(Stream input)
                {
                    using var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    int read;
                    while ((read = await input.ReadAsync(chunk)) > 0)
                    {
                        buffer.Write(chunk, 0, read);
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO002");
    }

    [Fact]
    public async Task Read_loop_with_total_checked_against_max_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(Stream input)
                {
                    using var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    long total = 0;
                    int read;
                    while ((read = await input.ReadAsync(chunk)) > 0)
                    {
                        total += read;
                        if (total > Max)
                        {
                            throw new InvalidDataException();
                        }

                        buffer.Write(chunk, 0, read);
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_loop_into_file_stream_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public async Task Accept(Stream input, Stream output)
                {
                    var chunk = new byte[4096];
                    int read;
                    while ((read = await input.ReadAsync(chunk)) > 0)
                    {
                        output.Write(chunk, 0, read);
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Do_loop_reading_into_memory_stream_reports_skio002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Intake
            {
                private const long Max = 1024;

                public void Accept(Stream input)
                {
                    using var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    int read;
                    do
                    {
                        read = input.Read(chunk, 0, chunk.Length);
                        buffer.Write(chunk, 0, read);
                    }
                    while (read != 0);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SKIO002");
    }
}
