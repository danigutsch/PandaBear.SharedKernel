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
        (hasItem).ShouldBeTrue();
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
        (hasItem).ShouldBeTrue();
        var firstItem = enumerator.Current;
        await cts.CancelAsync();

        async Task Act()
        {
            await enumerator.MoveNextAsync();
        }

        await ((Func<Task>)Act).ShouldThrowAssignableTo<OperationCanceledException>();
        return (firstItem, readTrace());
    }

    public static async Task<(string FirstItem, string[] Trace)> ThrowAfterFirstItemAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        var hasItem = await enumerator.MoveNextAsync();
        (hasItem).ShouldBeTrue();
        var firstItem = enumerator.Current;

        async Task Act()
        {
            await enumerator.MoveNextAsync();
        }

        var exception = await ((Func<Task>)Act).ShouldThrow<InvalidOperationException>();
        (exception.Message).ShouldBe("boom");
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
            (hasItem).ShouldBeTrue();
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
