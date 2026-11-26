using System.Text;
using System.Text.Json.Nodes;
using SharedKernel.DocumentRendering.Pdf;
using SharedKernel.Processes;

namespace SharedKernel.DocumentRendering.Tests;

/// <summary>Builds qpdf reply variations and checks numbered normalization rejection.</summary>
internal static class QpdfNumberedDocumentFixture
{
    /// <summary>Checks the error, inspection cutoff and scratch cleanup for invalid numbered input.</summary>
    internal static async Task RejectsNumberedInput(
        ReadOnlyMemory<byte> generatedPdf, string scenario, string expectedError, CancellationToken ct, params string[] replies)
    {
        var remaining = new Queue<string>(replies);
        string? scratch = null;
        var normalizer = new QpdfDocumentNormalizer(
            Path.Combine(Path.GetTempPath(), "qualified-qpdf"),
            4_096,
            TimeSpan.FromSeconds(10),
            (command, _, _) =>
            {
                scratch = command.WorkingDirectory;
                command.ArgumentList.Any(static argument => argument.StartsWith("--update-from-json=", StringComparison.Ordinal))
                    .ShouldBeFalse($"{scenario} must stop before qpdf rewrites the PDF.");
                var reply = Encoding.UTF8.GetBytes(remaining.Dequeue());
                return Task.FromResult(new CapturedProcessOutput(0, reply, string.Empty));
            });

        Func<Task> normalize = () => normalizer.NormalizeNumbered(generatedPdf, DateTimeOffset.UnixEpoch, ct);
        var failure = await normalize.ShouldThrow<InvalidDataException>();

        failure.Message.ShouldBe(expectedError, scenario);
        remaining.ShouldBeEmpty(scenario);
        Directory.Exists(scratch).ShouldBeFalse();
    }

    /// <summary>Applies a targeted edit to an independent copy of a fixture reply.</summary>
    internal static string Edit(string json, Action<JsonObject> edit)
    {
        var result = JsonNode.Parse(json)!.AsObject();
        edit(result);
        return result.ToJsonString();
    }

    /// <summary>Gets the first page dictionary in the fixture's selected-object reply.</summary>
    internal static JsonObject SelectedPage(JsonObject metadata) =>
        metadata["qpdf"]![1]!["obj:5 0 R"]!["value"]!.AsObject();
}
