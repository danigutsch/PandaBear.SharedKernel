using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>Reads the content of a language and records each read, so a test sees which language was read and with which token.</summary>
public sealed class CulturePageReader
{
    private readonly List<ContentCulture> _cultures = [];

    private readonly Lock _gate = new();

    /// <summary>Gets the languages read so far, in order.</summary>
    public IReadOnlyList<ContentCulture> Cultures
    {
        get
        {
            lock (_gate)
            {
                return [.. _cultures];
            }
        }
    }

    /// <summary>Gets a value indicating whether every read had a token that can be cancelled, such as the request's.</summary>
    public bool ReadWithCancellableToken { get; private set; } = true;

    /// <summary>Reads the content of a language.</summary>
    /// <param name="culture">The language.</param>
    /// <param name="cancellationToken">The token of the read.</param>
    /// <returns>The content.</returns>
    public Task<CulturePageContent> Read(ContentCulture culture, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _cultures.Add(culture);
            ReadWithCancellableToken &= cancellationToken.CanBeCanceled;
        }

        return Task.FromResult(new CulturePageContent(culture));
    }
}
