namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorDispatchBehaviorTestsHelpers
{
    public static async Task<(string FirstItem, string[] Trace)> ReadFirstItemAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        var hasItem = await enumerator.MoveNextAsync();
        Assert.True(hasItem);
        return (enumerator.Current, readTrace());
    }

    public static async Task<(string FirstItem, string[] Trace)> CancelAfterFirstItemAndTrace(
        Func<CancellationToken, IAsyncEnumerable<string>> createSource,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var enumerator = createSource(cts.Token).GetAsyncEnumerator(cts.Token);
        var hasItem = await enumerator.MoveNextAsync();
        Assert.True(hasItem);
        var firstItem = enumerator.Current;
        await cts.CancelAsync();

        async Task Act()
        {
            await enumerator.MoveNextAsync();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        return (firstItem, readTrace());
    }

    public static async Task<(string FirstItem, string[] Trace)> ThrowAfterFirstItemAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        var hasItem = await enumerator.MoveNextAsync();
        Assert.True(hasItem);
        var firstItem = enumerator.Current;

        async Task Act()
        {
            await enumerator.MoveNextAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Equal("boom", exception.Message);
        return (firstItem, readTrace());
    }

    public static async Task<(string FirstItem, string[] Trace)> ReadFirstItemThenDisposeAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        string firstItem;

        await using (var enumerator = source.GetAsyncEnumerator(ct))
        {
            var hasItem = await enumerator.MoveNextAsync();
            Assert.True(hasItem);
            firstItem = enumerator.Current;
        }

        return (firstItem, readTrace());
    }

    public static async Task<(string[] Items, string[] Trace)> CollectItemsAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace)
    {
        var items = await AsyncEnumerableTestHelper.Collect(source);
        return (items, readTrace());
    }
}
