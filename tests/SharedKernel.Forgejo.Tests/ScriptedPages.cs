namespace SharedKernel.Forgejo.Tests;

/// <summary>Answers each page request with the next scripted page and records the page numbers asked for.</summary>
/// <param name="pages">The pages, in the order they are answered.</param>
internal sealed class ScriptedPages(IReadOnlyList<ForgejoPage<int>> pages)
{
    /// <summary>Gets the page numbers in the order they were asked for.</summary>
    public List<int> Requested { get; } = [];

    /// <summary>Returns the next scripted page.</summary>
    public Task<ForgejoPage<int>> Read(int page, CancellationToken cancellationToken)
    {
        Requested.Add(page);
        return Task.FromResult(pages[Requested.Count - 1]);
    }

    /// <summary>Returns a page of <paramref name="count"/> items with the given paging headers.</summary>
    public static ForgejoPage<int> Page(int count, long? total = null, string? link = null) =>
        new([.. Enumerable.Range(0, count)], link?.Trim(), total);
}
