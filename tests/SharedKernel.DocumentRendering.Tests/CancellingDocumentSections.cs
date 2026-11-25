using System.Collections;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Cancels its caller when rendering reaches the first section.</summary>
/// <param name="section">The document section supplied for rendering.</param>
/// <param name="cancellation">The caller's cancellation source.</param>
internal sealed class CancellingDocumentSections(DocumentSection section, CancellationTokenSource cancellation) : IReadOnlyList<DocumentSection>
{
    /// <inheritdoc />
    public int Count => 1;

    /// <inheritdoc />
    public DocumentSection this[int index] => index == 0 ? section : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc />
    public IEnumerator<DocumentSection> GetEnumerator()
    {
        cancellation.Cancel();
        yield return section;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
