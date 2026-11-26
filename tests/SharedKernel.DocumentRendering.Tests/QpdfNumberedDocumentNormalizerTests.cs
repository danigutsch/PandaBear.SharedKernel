using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedKernel.DocumentRendering.Pdf;
using SharedKernel.Processes;
using SharedKernel.Testing;
using static SharedKernel.DocumentRendering.Tests.QpdfNumberedDocumentFixture;

namespace SharedKernel.DocumentRendering.Tests;

/// <summary>Verifies the bounded selective object update for numbered rendering.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class QpdfNumberedDocumentNormalizerTests
{
    private const string UnsupportedPageMetadata = "qpdf did not return supported page metadata for a fresh Chromium A4 PDF.";
    private const string UnsupportedDocumentMetadata = "qpdf did not return supported metadata for a fresh Chromium tagged PDF.";

    private const string Trailer = """
        {
          "pages": [
            {"object":"5 0 R","pageposfrom1":1,"contents":["11 0 R"]},
            {"object":"6 0 R","pageposfrom1":2,"contents":["15 0 R","17 0 R"]}
          ],
          "qpdf":[{"jsonversion":2,"pdfversion":"1.4"},{"trailer":{"value":{"/Info":"7 2 R","/Root":"9 0 R"}}}]
        }
        """;

    private const string Metadata = """
        {
          "qpdf":[
            {"jsonversion":2,"pdfversion":"1.4","maxobjectid":186},
            {
              "obj:7 2 R":{"value":{"/Creator":"u:Chromium","/Producer":"u:Skia/PDF m156","/Title":"u:Example","/CreationDate":"u:D:20261123070000+00'00'","/ModDate":"u:D:20261123070000+00'00'"}},
              "obj:9 0 R":{"value":{"/Lang":"u:pt-BR","/MarkInfo":{"/Marked":true},"/StructTreeRoot":"33 0 R"}},
              "obj:5 0 R":{"value":{"/Type":"/Page","/MediaBox":[0,0,594.95996,841.91998],"/Contents":"11 0 R","/StructParents":0}},
              "obj:6 0 R":{"value":{"/Type":"/Page","/MediaBox":[0,0,594.95996,841.91998],"/Contents":["15 0 R","17 0 R"],"/StructParents":1,"/Annots":["16 0 R"]}}
            }
          ]
        }
        """;

    private static readonly byte[] GeneratedPdf = "%PDF-1.4\nsynthetic Chromium fixture"u8.ToArray();

    /// <summary>Supplies malformed or inconsistent page lists for the trailer inspection.</summary>
    public static TheoryData<string, string, string> InvalidPageLists => new()
    {
        { "unsupported PDF version", Edit(Trailer, root => root["qpdf"]![0]!["pdfversion"] = "1.5"), UnsupportedPageMetadata },
        { "non-array page list", Edit(Trailer, root => root["pages"] = new JsonObject()), UnsupportedPageMetadata },
        { "empty page list", Edit(Trailer, root => root["pages"] = new JsonArray()), UnsupportedPageMetadata },
        {
            "more than 128 pages",
            Edit(Trailer, root => root["pages"] = new JsonArray([.. Enumerable.Range(0, 129).Select(static _ => new JsonObject())])),
            UnsupportedPageMetadata
        },
        { "malformed page", Edit(Trailer, root => root["pages"]![0] = "page"), UnsupportedPageMetadata },
        { "missing page position", Edit(Trailer, root => root["pages"]![0]!["pageposfrom1"] = null), UnsupportedPageMetadata },
        { "non-integral page position", Edit(Trailer, root => root["pages"]![0]!["pageposfrom1"] = "first"), UnsupportedPageMetadata },
        { "out-of-order position", Edit(Trailer, root => root["pages"]![0]!["pageposfrom1"] = 2), UnsupportedPageMetadata },
        { "non-array contents", Edit(Trailer, root => root["pages"]![0]!["contents"] = "11 0 R"), UnsupportedPageMetadata },
        { "empty contents", Edit(Trailer, root => root["pages"]![0]!["contents"] = new JsonArray()), UnsupportedPageMetadata },
        {
            "more than 16 original content streams",
            Edit(Trailer, root => root["pages"]![0]!["contents"] =
                new JsonArray([.. Enumerable.Range(0, 17).Select(static _ => JsonValue.Create("11 0 R"))])),
            UnsupportedPageMetadata
        },
        { "duplicate page reference", Edit(Trailer, root => root["pages"]![1]!["object"] = "5 0 R"), UnsupportedPageMetadata },
        { "invalid page reference", Edit(Trailer, root => root["pages"]![0]!["object"] = "0 0 R"), UnsupportedDocumentMetadata },
        { "invalid content reference", Edit(Trailer, root => root["pages"]![0]!["contents"]![0] = "11 -1 R"), UnsupportedDocumentMetadata },
    };

    /// <summary>Supplies inconsistent document object replies for the selected-object inspection.</summary>
    public static TheoryData<string, string> InvalidSelectedObjects => new()
    {
        { "unsupported PDF version", Edit(Metadata, root => root["qpdf"]![0]!["pdfversion"] = "1.5") },
        { "missing object limit", Edit(Metadata, root => root["qpdf"]![0]!["maxobjectid"] = null) },
        { "non-integral object limit", Edit(Metadata, root => root["qpdf"]![0]!["maxobjectid"] = "max") },
        { "zero object limit", Edit(Metadata, root => root["qpdf"]![0]!["maxobjectid"] = 0) },
        { "no available object IDs", Edit(Metadata, root => root["qpdf"]![0]!["maxobjectid"] = int.MaxValue) },
        { "missing selected page", Edit(Metadata, root => root["qpdf"]![1]!["obj:5 0 R"] = null) },
        { "missing page dictionary", Edit(Metadata, root => root["qpdf"]![1]!["obj:5 0 R"]!["value"] = null) },
        { "invalid page type", Edit(Metadata, root => SelectedPage(root)["/Type"] = "/Pages") },
        { "missing page box", Edit(Metadata, root => SelectedPage(root)["/MediaBox"] = null) },
        { "incomplete page box", Edit(Metadata, root => SelectedPage(root)["/MediaBox"] = new JsonArray(0, 0, 595)) },
        { "non-number left edge", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![0] = "left") },
        { "non-number bottom edge", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![1] = "bottom") },
        { "nonzero bottom edge", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![1] = 1) },
        { "non-number page width", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![2] = "width") },
        { "box below the A4 width limit", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![2] = 590) },
        { "non-number page height", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![3] = "height") },
        { "box below the A4 height limit", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![3] = 830) },
        { "box above the A4 height limit", Edit(Metadata, root => SelectedPage(root)["/MediaBox"]![3] = 850) },
        { "missing content reference", Edit(Metadata, root => SelectedPage(root)["/Contents"] = null) },
        { "changed content reference", Edit(Metadata, root => SelectedPage(root)["/Contents"] = "12 0 R") },
    };

    [Fact]
    public async Task Normalize_numbered_prepends_separate_artifacts_and_preserves_original_page_objects()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var commands = 0;
        string? scratch = null;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            async (command, outputLimit, token) =>
            {
                commands++;
                scratch = command.WorkingDirectory;
                if (command.ArgumentList.Contains("--json-object=trailer"))
                {
                    command.ArgumentList.ShouldContain("--json-key=pages");
                    outputLimit.ShouldBe(65_536);
                    return new CapturedProcessOutput(0, Encoding.UTF8.GetBytes(Trailer), string.Empty);
                }

                if (command.ArgumentList.Contains("--json-object=7,2"))
                {
                    command.ArgumentList.ShouldContain("--json-object=5,0");
                    command.ArgumentList.ShouldContain("--json-object=6,0");
                    command.ArgumentList.ShouldContain("--json-object=9,0");
                    command.ArgumentList.ShouldNotContain("--json-object=11,0");
                    outputLimit.ShouldBe(65_536);
                    return new CapturedProcessOutput(0, Encoding.UTF8.GetBytes(Metadata), string.Empty);
                }

                command.ArgumentList.ShouldContain("--stream-data=preserve");
                command.ArgumentList.ShouldContain("--object-streams=preserve");
                command.ArgumentList.ShouldContain("--deterministic-id");
                outputLimit.ShouldBe(4_096);
                const string updateOption = "--update-from-json=";
                var parameter = command.ArgumentList.ShouldHaveSingleItem(arg => arg.StartsWith(updateOption, StringComparison.Ordinal));
                using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(parameter[updateOption.Length..], token));
                var edits = document.RootElement.GetProperty("qpdf")[1];
                var firstPage = edits.GetProperty("obj:5 0 R").GetProperty("value");
                firstPage.GetProperty("/Contents").ToString().ShouldBe("""["187 0 R","11 0 R"]""");
                firstPage.GetProperty("/StructParents").GetInt32().ShouldBe(0);
                var secondPage = edits.GetProperty("obj:6 0 R").GetProperty("value");
                secondPage.GetProperty("/Contents").ToString().ShouldBe("""["188 0 R","15 0 R","17 0 R"]""");
                secondPage.GetProperty("/StructParents").GetInt32().ShouldBe(1);
                secondPage.GetProperty("/Annots").ToString().ShouldBe("""["16 0 R"]""");
                edits.TryGetProperty("obj:16 0 R", out _).ShouldBeFalse();
                edits.TryGetProperty("obj:9 0 R", out _).ShouldBeFalse();
                edits.GetProperty("obj:7 2 R").GetProperty("value").GetProperty("/CreationDate").GetString().ShouldBe("u:D:19700101000000+00'00'");

                var firstStream = edits.GetProperty("obj:187 0 R").GetProperty("stream");
                var secondStream = edits.GetProperty("obj:188 0 R").GetProperty("stream");
                firstStream.GetProperty("datafile").GetString().ShouldBe("page-number-1.stream");
                secondStream.GetProperty("datafile").GetString().ShouldBe("page-number-2.stream");
                var first = await File.ReadAllTextAsync(Path.Combine(command.WorkingDirectory, "page-number-1.stream"), token);
                var second = await File.ReadAllTextAsync(Path.Combine(command.WorkingDirectory, "page-number-2.stream"), token);
                first.ShouldContain("q\n/Artifact << /Type /Pagination /Attached [/Bottom] >> BDC\n", StringComparison.Ordinal);
                first.ShouldContain("1 0 0 1 294.48 16 cm\n", StringComparison.Ordinal);
                first.ShouldContain("EMC\nQ\n", StringComparison.Ordinal);
                second.ShouldNotBe(first);
                return new CapturedProcessOutput(0, GeneratedPdf, string.Empty);
            });

        // Act
        var result = await normalizer.NormalizeNumbered(GeneratedPdf, DateTimeOffset.UnixEpoch, ct);

        // Assert
        result.ToArray().ShouldBe(GeneratedPdf);
        commands.ShouldBe(3);
        Directory.Exists(scratch).ShouldBeFalse();
    }

    [Theory]
    [InlineData("\"/MediaBox\":[1,0,594.95996,841.91998]")]
    [InlineData("\"/MediaBox\":[0,0,612,792]")]
    [InlineData("\"/Rotate\":90,\"/MediaBox\":[0,0,594.95996,841.91998]")]
    [InlineData("\"/CropBox\":[0,0,590,840],\"/MediaBox\":[0,0,594.95996,841.91998]")]
    [InlineData("\"/UserUnit\":2,\"/MediaBox\":[0,0,594.95996,841.91998]")]
    public async Task Normalize_numbered_rejects_unsupported_page_geometry_before_rewriting(string geometry)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var commands = 0;
        const string mediaBox = "\"/MediaBox\":[0,0,594.95996,841.91998]";
        var invalidMetadata = Metadata.Replace(mediaBox, geometry, StringComparison.Ordinal);
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            (_, _, _) =>
            {
                commands++;
                var json = commands == 1 ? Trailer : invalidMetadata;
                return Task.FromResult(new CapturedProcessOutput(0, Encoding.UTF8.GetBytes(json), string.Empty));
            });

        // Act
        Func<Task> normalize = () => normalizer.NormalizeNumbered(GeneratedPdf, DateTimeOffset.UnixEpoch, ct);
        var failure = await normalize.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe(UnsupportedPageMetadata);
        commands.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(InvalidPageLists))]
    public async Task Normalize_numbered_rejects_invalid_page_lists_before_selecting_objects(
        string scenario, string invalidTrailer, string expectedError)
    {
        // Act and assert
        await RejectsNumberedInput(GeneratedPdf, scenario, expectedError, TestContext.Current.CancellationToken, invalidTrailer);
    }

    [Theory]
    [MemberData(nameof(InvalidSelectedObjects))]
    public async Task Normalize_numbered_rejects_inconsistent_selected_page_objects_before_rewriting(
        string scenario, string invalidObjects)
    {
        // Act and assert
        await RejectsNumberedInput(
            GeneratedPdf, scenario, UnsupportedPageMetadata, TestContext.Current.CancellationToken, Trailer, invalidObjects);
    }
}
