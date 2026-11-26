using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.DocumentRendering.Pdf;

/// <summary>Normalizes the volatile Info dates of a renderer-owned tagged Chromium PDF with a caller-provisioned qpdf.</summary>
/// <remarks>Use only with fresh output from a qualified renderer. This API does not accept an uploaded PDF.</remarks>
public sealed class QpdfDocumentNormalizer
{
    private const int MaxMetadataBytes = 65_536;
    private const int MaxDiagnosticBytes = 8_192;
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);

    private readonly string _executablePath;
    private readonly int _maxPdfBytes;
    private readonly TimeSpan _timeout;
    private readonly Func<ProcessStartInfo, int, CancellationToken, Task<CapturedProcessOutput>> _capture;

    /// <summary>Creates a normalizer for the caller's pinned qpdf runtime.</summary>
    /// <param name="executablePath">The fully-qualified path to the trusted qpdf executable.</param>
    /// <param name="maxPdfBytes">The byte limit for both the input and rewritten document.</param>
    /// <param name="timeout">The time allowed for file I/O and the qpdf invocations.</param>
    /// <exception cref="ArgumentException"><paramref name="executablePath"/> is not fully qualified.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The byte limit or timeout is outside the supported range.</exception>
    public QpdfDocumentNormalizer(string executablePath, int maxPdfBytes, TimeSpan timeout)
        : this(executablePath, maxPdfBytes, timeout, CreateCapture(timeout))
    {
    }

    /// <summary>Creates a normalizer with a replaceable process boundary for testing.</summary>
    /// <exception cref="ArgumentException"><paramref name="executablePath"/> is not fully qualified.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The byte limit or timeout is outside the supported range.</exception>
    internal QpdfDocumentNormalizer(
        string executablePath,
        int maxPdfBytes,
        TimeSpan timeout,
        Func<ProcessStartInfo, int, CancellationToken, Task<CapturedProcessOutput>> capture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("The qpdf executable path must be fully qualified.", nameof(executablePath));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPdfBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout, MaxTimerDelay);
        _executablePath = executablePath;
        _maxPdfBytes = maxPdfBytes;
        _timeout = timeout;
        _capture = capture;
    }

    /// <summary>Replaces the generated PDF's two Info dates and writes a deterministic identifier.</summary>
    /// <param name="generatedPdf">Fresh, renderer-owned bytes within the configured limit.</param>
    /// <param name="documentDate">An immutable document date. qpdf receives its UTC value to second precision.</param>
    /// <param name="ct">Cancels the normalization and terminates a running qpdf process.</param>
    /// <returns>The bounded rewritten PDF bytes.</returns>
    /// <exception cref="InvalidDataException">The input or its metadata is outside the qualified shape, or qpdf rejects it.</exception>
    /// <exception cref="InvalidOperationException">The process could not run or exceeded a capture limit.</exception>
    /// <exception cref="IOException">The private temporary files could not be written or cleaned up.</exception>
    /// <exception cref="TimeoutException">The time limit elapsed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public Task<ReadOnlyMemory<byte>> Normalize(ReadOnlyMemory<byte> generatedPdf, DateTimeOffset documentDate, CancellationToken ct) =>
        NormalizeCore(generatedPdf, documentDate, numberPages: false, ct);

    /// <summary>Normalizes tagged Chromium A4 output and adds page numbers to its reserved bottom margin.</summary>
    /// <remarks>The caller reserves 20 mm for the artifact before Chromium prints the PDF.</remarks>
    /// <exception cref="InvalidDataException">The numbered document or its metadata is outside the qualified shape.</exception>
    /// <exception cref="InvalidOperationException">The process could not run or exceeded a capture limit.</exception>
    /// <exception cref="IOException">The private temporary files could not be written or cleaned up.</exception>
    /// <exception cref="TimeoutException">The time limit elapsed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    internal Task<ReadOnlyMemory<byte>> NormalizeNumbered(ReadOnlyMemory<byte> generatedPdf, DateTimeOffset documentDate, CancellationToken ct) =>
        NormalizeCore(generatedPdf, documentDate, numberPages: true, ct);

    private async Task<ReadOnlyMemory<byte>> NormalizeCore(
        ReadOnlyMemory<byte> generatedPdf,
        DateTimeOffset documentDate,
        bool numberPages,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (generatedPdf.Length > _maxPdfBytes || !generatedPdf.Span.StartsWith("%PDF-"u8))
        {
            throw new InvalidDataException("The generated PDF does not have a supported bounded input.");
        }

        using var deadline = new CancellationTokenSource(_timeout);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var scratch = Directory.CreateTempSubdirectory("sharedkernel-generated-pdf-");
        try
        {
            var input = Path.Combine(scratch.FullName, "input.pdf");
            await File.WriteAllBytesAsync(input, generatedPdf, execution.Token).ConfigureAwait(false);
            var trailer = await Inspect(scratch.FullName, input,
                numberPages ? ["--json-key=pages", "--json-object=trailer"] : ["--json-object=trailer"],
                execution.Token).ConfigureAwait(false);
            var (infoReference, catalogReference) = QpdfDocumentMetadata.References(trailer);
            var pages = numberPages ? QpdfPageNumbering.Read(trailer) : null;
            var metadata = await Inspect(scratch.FullName, input,
                [QpdfDocumentMetadata.SelectObject(infoReference), QpdfDocumentMetadata.SelectObject(catalogReference), .. (pages?.Selectors() ?? [])],
                execution.Token).ConfigureAwait(false);
            var update = Path.Combine(scratch.FullName, "dates.json");
            var json = pages is null
                ? QpdfDocumentMetadata.UpdateDates(metadata, infoReference, catalogReference, documentDate)
                : await pages.WriteUpdate(scratch.FullName, metadata, infoReference, catalogReference, documentDate, execution.Token).ConfigureAwait(false);
            await File.WriteAllTextAsync(update, json, execution.Token).ConfigureAwait(false);
            var output = await Run(scratch.FullName,
                ["--suppress-recovery", "--stream-data=preserve", "--object-streams=preserve", "--deterministic-id",
                    $"--update-from-json={update}", input, "-"],
                _maxPdfBytes, execution.Token).ConfigureAwait(false);
            return output.Span.StartsWith("%PDF-"u8)
                ? output
                : throw new InvalidDataException("qpdf did not return a generated PDF.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Generated PDF normalization exceeded its time limit.");
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private Task<ReadOnlyMemory<byte>> Inspect(string workingDirectory, string input, string[] filters, CancellationToken ct) =>
        Run(workingDirectory, ["--suppress-recovery", "--json=2", "--json-key=qpdf", .. filters, input], MaxMetadataBytes, ct);

    private async Task<ReadOnlyMemory<byte>> Run(string workingDirectory, string[] arguments, int maxOutputBytes, CancellationToken ct)
    {
        var command = new ProcessStartInfo(_executablePath) { WorkingDirectory = workingDirectory };
        foreach (var argument in arguments)
        {
            command.ArgumentList.Add(argument);
        }

        var result = await _capture(command, maxOutputBytes, ct).ConfigureAwait(false);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new InvalidDataException("qpdf could not normalize the generated PDF.");
    }

    private static Func<ProcessStartInfo, int, CancellationToken, Task<CapturedProcessOutput>> CreateCapture(TimeSpan timeout)
    {
        var runner = new BoundedProcessRunner(timeout);
        return (command, outputLimit, ct) => runner.Capture(command, ReadOnlyMemory<byte>.Empty, outputLimit, MaxDiagnosticBytes, ct);
    }
}
