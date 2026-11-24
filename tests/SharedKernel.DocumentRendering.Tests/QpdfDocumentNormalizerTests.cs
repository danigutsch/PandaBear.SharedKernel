using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PandaBear.DocumentRendering.Pdf;
using PandaBear.Processes;
using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Verifies the bounded metadata rewrite for renderer-owned tagged documents.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class QpdfDocumentNormalizerTests
{
    private const string ChromiumMetadata = """
        {"qpdf":[{"jsonversion":2},{"obj:7 2 R":{"value":{"/Creator":"u:Chromium","/Producer":"u:Skia/PDF m156","/Title":"u:Contrato sintético","/CreationDate":"u:D:20261123070000+00'00'","/ModDate":"u:D:20261123070000+00'00'"}},"obj:9 0 R":{"value":{"/Lang":"u:pt-BR","/MarkInfo":{"/Marked":true},"/StructTreeRoot":"11 0 R"}}}]}
        """;
    private static readonly byte[] GeneratedPdf = "%PDF-1.4\nsynthetic Chromium fixture"u8.ToArray();
    private static readonly byte[] RewrittenPdf = "%PDF-1.4\nrewritten fixture"u8.ToArray();

    [Fact]
    public async Task Normalize_uses_the_caller_UTC_date_and_preserves_the_other_info_fields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var commandCount = 0;
        string? privateDirectory = null;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            async (command, outputLimit, token) =>
            {
                commandCount++;
                privateDirectory = command.WorkingDirectory;
                command.ArgumentList.ShouldContain("--suppress-recovery");
                if (command.ArgumentList.Contains("--json-object=trailer"))
                {
                    return new CapturedProcessOutput(0, """
                    {"qpdf":[{"jsonversion":2},{"trailer":{"value":{"/Info":"7 2 R","/Root":"9 0 R"}}}]}
                    """u8.ToArray(), string.Empty);
                }

                if (command.ArgumentList.Contains("--json-object=7,2"))
                {
                    command.ArgumentList.ShouldContain("--json-object=9,0");
                    return new CapturedProcessOutput(0, Encoding.UTF8.GetBytes(ChromiumMetadata), string.Empty);
                }

                outputLimit.ShouldBe(4_096);
                command.ArgumentList.ShouldContain("--deterministic-id");
                command.ArgumentList[^1].ShouldBe("-");
                const string updateOption = "--update-from-json=";
                var update = command.ArgumentList.ShouldHaveSingleItem(arg => arg.StartsWith(updateOption, StringComparison.Ordinal));
                using var data = JsonDocument.Parse(await File.ReadAllBytesAsync(update[updateOption.Length..], token));
                var edited = data.RootElement.GetProperty("qpdf")[1];
                var info = edited.GetProperty("obj:7 2 R").GetProperty("value");
                info.GetProperty("/CreationDate").GetString().ShouldBe("u:D:20261101123000+00'00'");
                info.GetProperty("/ModDate").GetString().ShouldBe("u:D:20261101123000+00'00'");
                info.GetProperty("/Title").GetString().ShouldBe("u:Contrato sintético");
                info.GetProperty("/Producer").GetString().ShouldBe("u:Skia/PDF m156");
                edited.TryGetProperty("obj:9 0 R", out _).ShouldBeFalse();
                return new CapturedProcessOutput(0, RewrittenPdf, string.Empty);
            });
        var date = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.FromHours(-3));

        // Act
        var result = await normalizer.Normalize(GeneratedPdf, date, ct);

        // Assert
        result.ToArray().ShouldBe(RewrittenPdf);
        commandCount.ShouldBe(3);
        Directory.Exists(privateDirectory).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not pdf", 4_096)]
    [InlineData("%PDF-1.4", 6)]
    public async Task Normalize_rejects_invalid_input_before_starting_qpdf(string input, int limit)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            limit,
            TimeSpan.FromSeconds(10),
            static (_, _, _) => throw new InvalidOperationException("The process must not be started."));
        var content = Encoding.UTF8.GetBytes(input);

        // Act
        Func<Task> normalization = () => normalizer.Normalize(content, DateTimeOffset.UnixEpoch, ct);
        var failure = await normalization.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe("The generated PDF does not have a supported bounded input.");
    }

    [Fact]
    public async Task Normalize_refuses_qpdf_warnings_without_exposing_document_diagnostics()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        string? scratch = null;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            (command, _, _) =>
            {
                scratch = command.WorkingDirectory;
                return Task.FromResult(new CapturedProcessOutput(3, GeneratedPdf, "private title in qpdf warning"));
            });

        // Act
        Func<Task> normalization = () => normalizer.Normalize(GeneratedPdf, DateTimeOffset.UnixEpoch, ct);
        var failure = await normalization.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe("qpdf could not normalize the generated PDF.");
        Directory.Exists(scratch).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("[]")]
    [InlineData("""{"qpdf":{}}""")]
    [InlineData("""{"qpdf":[{},{}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":"2"},{}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":1},{}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":2},{"trailer":{"stream":{}}}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":2},{"trailer":{"value":{"/Info":"@private","/Root":"9 0 R"}}}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":2},{"trailer":{"value":{"/Info":"0 0 R","/Root":"9 0 R"}}}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":2},{"trailer":{"value":{"/Info":"1 -1 R","/Root":"9 0 R"}}}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":2},{"trailer":{"value":{"/Info":"1 0 R","/Root":"9 0 R","/Encrypt":"2 0 R"}}}]}""")]
    [InlineData("""{"qpdf":[{"jsonversion":2},{"trailer":{"value":{"/Info":"1 0 R","/Root":"9 0 R","/ID":["b:ff","b:aa"]}}}]}""")]
    public async Task Normalize_refuses_trailer_metadata_outside_the_renderer_contract(string trailer)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var processCalls = 0;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            (_, _, _) =>
            {
                processCalls++;
                return Task.FromResult(new CapturedProcessOutput(0, Encoding.UTF8.GetBytes(trailer), string.Empty));
            });

        // Act
        Func<Task> normalization = () => normalizer.Normalize(GeneratedPdf, DateTimeOffset.UnixEpoch, ct);
        var failure = await normalization.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe("qpdf did not return supported metadata for a fresh Chromium tagged PDF.");
        processCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("obj:9 0 R", "/Metadata", "\"8 0 R\"")]
    [InlineData("obj:9 0 R", "/MarkInfo", "null")]
    [InlineData("obj:9 0 R", "/MarkInfo", """{"/Marked":"true"}""")]
    [InlineData("obj:9 0 R", "/MarkInfo", """{"/Marked":false}""")]
    [InlineData("obj:9 0 R", "/Lang", "\"u:\"")]
    [InlineData("obj:9 0 R", "/Lang", "\"b:656e\"")]
    [InlineData("obj:9 0 R", "/StructTreeRoot", "null")]
    [InlineData("obj:7 2 R", "/Title", "[]")]
    [InlineData("obj:7 2 R", "/Creator", "\"u:Other producer\"")]
    [InlineData("obj:7 2 R", "/Producer", "\"u:Other renderer\"")]
    [InlineData("obj:7 2 R", "/CreationDate", "\"u:date\"")]
    [InlineData("obj:7 2 R", "/ModDate", "null")]
    public void Update_dates_refuses_metadata_outside_the_qualified_shape(string objectKey, string field, string value)
    {
        // Arrange
        var metadata = JsonNode.Parse(ChromiumMetadata).ShouldNotBeNull();
        var dictionary = metadata["qpdf"]![1]![objectKey]!["value"]!.AsObject();
        dictionary[field] = JsonNode.Parse(value);
        var encoded = Encoding.UTF8.GetBytes(metadata.ToJsonString());

        // Act
        Action update = () => QpdfDocumentMetadata.UpdateDates(encoded, "7 2 R", "9 0 R", DateTimeOffset.UnixEpoch);
        var failure = update.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe("qpdf did not return supported metadata for a fresh Chromium tagged PDF.");
    }

    [Fact]
    public void Constructor_requires_an_absolute_qpdf_path()
    {
        // Arrange
        Action create = () => _ = new QpdfDocumentNormalizer("qpdf", 4_096, TimeSpan.FromSeconds(10));

        // Act
        var failure = create.ShouldThrow<ArgumentException>();

        // Assert
        failure.ParamName.ShouldBe("executablePath");
    }

    [Fact]
    public async Task Normalize_releases_the_private_directory_after_cancellation()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string? scratch = null;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            async (command, _, ct) =>
            {
                scratch = command.WorkingDirectory;
                await cancellation.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return new CapturedProcessOutput(0, GeneratedPdf, string.Empty);
            });

        // Act
        Func<Task> normalization = () => normalizer.Normalize(GeneratedPdf, DateTimeOffset.UnixEpoch, cancellation.Token);
        await normalization.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        Directory.Exists(scratch).ShouldBeFalse();
    }

    [Fact]
    public async Task Normalize_reports_its_deadline_when_the_caller_still_waits()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var unfinished = new TaskCompletionSource<CapturedProcessOutput>(TaskCreationOptions.RunContinuationsAsynchronously);
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromMilliseconds(500),
            (_, _, token) => unfinished.Task.WaitAsync(token));

        // Act
        Func<Task> normalization = () => normalizer.Normalize(GeneratedPdf, DateTimeOffset.UnixEpoch, ct);
        var failure = await normalization.ShouldThrow<TimeoutException>();

        // Assert
        failure.Message.ShouldBe("Generated PDF normalization exceeded its time limit.");
    }
}
