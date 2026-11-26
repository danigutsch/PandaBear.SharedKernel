using System.Diagnostics;
using Microsoft.Playwright;
using SharedKernel.DocumentRendering.Pdf.Chromium;
using SharedKernel.Processes;

namespace SharedKernel.DocumentRendering.Tests;

/// <summary>Supplies the locally qualified runtime, fixtures, and artifact directory.</summary>
internal static class ChromiumPdfTestRuntime
{
    /// <summary>Gets the fixture's immutable document date.</summary>
    internal static DateTimeOffset DocumentDate { get; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the fixture's embedded PNG logo.</summary>
    internal static Uri EmbeddedLogo { get; } =
        new("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMB/6X8w+cAAAAASUVORK5CYII=");

    /// <summary>Starts the caller-provisioned Chromium executable.</summary>
    internal static Task<IBrowser> Launch(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new() { ExecutablePath = GetEnvironment("PANDABEAR_TEST_CHROMIUM") });

    /// <summary>Creates the renderer with the caller-provisioned qpdf.</summary>
    internal static ChromiumDocumentRenderer QualifiedRenderer(IBrowser browser) =>
        Renderer(browser, GetEnvironment("PANDABEAR_TEST_QPDF"));

    /// <summary>Creates a renderer for a configured qpdf path and artifact bound.</summary>
    internal static ChromiumDocumentRenderer Renderer(IBrowser browser, string qpdfPath, int maxPdfBytes = 262_144) =>
        new(browser, qpdfPath, maxHtmlBytes: 65_536, maxPdfBytes, TimeSpan.FromSeconds(15));

    /// <summary>Creates a synthetic document in the requested language.</summary>
    internal static DocumentRenderRequest Document(string language, string paragraph, Uri? logo) =>
        new(language, "Travel service contract 0284",
            [
                new DocumentSection("Contract details", [])
                {
                    Paragraphs = [new DocumentParagraph(paragraph, DocumentPrivacyClassification.Public)],
                    Lists = [new DocumentList(["Meet the guide at the station."], DocumentPrivacyClassification.Public)],
                    Blocks =
                    [
                        new DocumentLink(
                            language == "pt-BR" ? "Leia os termos da viagem" : "Read the travel terms",
                            new Uri("https://example.invalid/travel-terms"),
                            DocumentPrivacyClassification.Public),
                    ],
                },
            ],
            logo is null ? null : new DocumentBrandingSnapshot("1", "Fixture brand", logo));

    /// <summary>Creates the same sealed typed content for default and numbered multipage qualification.</summary>
    internal static DocumentRenderRequest MultipageDocument()
    {
        var fixture = Document("pt-BR", "A visita ao café está incluída.", EmbeddedLogo);
        var section = fixture.Sections.ShouldHaveSingleItem();
        return fixture with
        {
            Title = "Contrato de viagem",
            Sections = [.. Enumerable.Range('A', 24).Select(code => section with { Heading = $"Etapa {(char)code}" })],
        };
    }

    /// <summary>Writes a fixture result to the caller's artifact directory and returns its path.</summary>
    internal static async Task<string> Save(string name, ReadOnlyMemory<byte> artifact, CancellationToken ct)
    {
        var destination = GetEnvironment("PANDABEAR_TEST_CHROMIUM_ARTIFACTS");
        Directory.CreateDirectory(destination);
        var path = Path.GetFullPath(Path.Combine(destination, name + ".pdf"));
        await File.WriteAllBytesAsync(path, artifact, ct);
        return path;
    }

    /// <summary>Inspects a saved fixture with the caller-provisioned qpdf executable.</summary>
    internal static async Task<ReadOnlyMemory<byte>> Inspect(string path, string[] arguments, CancellationToken ct)
    {
        var command = new ProcessStartInfo(GetEnvironment("PANDABEAR_TEST_QPDF"));
        command.ArgumentList.Add("--suppress-recovery");
        foreach (var argument in arguments)
        {
            command.ArgumentList.Add(argument);
        }

        command.ArgumentList.Add(path);
        var runner = new BoundedProcessRunner(TimeSpan.FromSeconds(15));
        var result = await runner.Capture(command, ReadOnlyMemory<byte>.Empty, 65_536, 8_192, ct);
        result.ExitCode.ShouldBe(0);
        return result.StandardOutput;
    }

    private static string GetEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name).ShouldNotBeNull($"Provide {name} for the Chromium PDF qualification.");
}
