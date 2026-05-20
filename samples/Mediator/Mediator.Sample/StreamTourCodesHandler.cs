using System.Runtime.CompilerServices;
using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Streams a small sequence of tour codes for the sample flow.
/// </summary>
public sealed class StreamTourCodesHandler : IStreamRequestHandler<StreamTourCodes, string>
{
    /// <inheritdoc />
    public IAsyncEnumerable<string> Handle(StreamTourCodes request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        return StreamTourCodes(request.Count, ct);
    }

    private static async IAsyncEnumerable<string> StreamTourCodes(int count, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var index = 1; index <= count; index++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return $"VT-{index:D3}";
        }
    }
}
