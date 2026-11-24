using PandaBear.DocumentRendering.Pdf;
using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Runs the optional qualification against caller-provisioned synthetic Chromium fixtures and qpdf.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class QualifiedQpdfTests
{
    [Theory(Explicit = true)]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    public async Task Qualified_tagged_documents_from_different_render_times_have_identical_rewritten_bytes(string language)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var qpdf = Environment.GetEnvironmentVariable("PANDABEAR_TEST_QPDF").ShouldNotBeNull("Provide the pinned qpdf executable.");
        var fixtures = Environment.GetEnvironmentVariable("PANDABEAR_TEST_QPDF_FIXTURES").ShouldNotBeNull("Provide the first and second fixture directories.");
        var artifacts = Environment.GetEnvironmentVariable("PANDABEAR_TEST_QPDF_ARTIFACTS").ShouldNotBeNull("Provide an output directory for the qualified PDF files.");
        var first = await File.ReadAllBytesAsync(Path.Combine(fixtures, "first", language + ".pdf"), ct);
        var second = await File.ReadAllBytesAsync(Path.Combine(fixtures, "second", language + ".pdf"), ct);
        first.AsSpan().SequenceEqual(second).ShouldBeFalse();
        var normalizer = new QpdfDocumentNormalizer(qpdf, 262_144, TimeSpan.FromSeconds(20));
        var documentDate = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var firstOutput = await normalizer.Normalize(first, documentDate, ct);
        var secondOutput = await normalizer.Normalize(second, documentDate, ct);

        // Assert
        firstOutput.Span.SequenceEqual(secondOutput.Span).ShouldBeTrue();
        Directory.CreateDirectory(artifacts);
        await File.WriteAllBytesAsync(Path.Combine(artifacts, language + ".pdf"), firstOutput, ct);
    }
}
