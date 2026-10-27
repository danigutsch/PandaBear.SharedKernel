namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Helpers that enumerate a stream and capture the handler trace for dispatch behavior tests.</summary>
internal static class GeneratorDispatchBehaviorTestsHelpers
{
    /// <summary>Moves the stream to its first item, asserts that one exists, and returns that item with the trace recorded so far.</summary>
    public static async Task<(string FirstItem, string[] Trace)> ReadFirstItemAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        var hasItem = await enumerator.MoveNextAsync();
        hasItem.ShouldBeTrue();
        return (enumerator.Current, readTrace());
    }

    /// <summary>Reads the first item, cancels the linked token, asserts that the next MoveNextAsync throws an OperationCanceledException, and returns the first item with the trace.</summary>
    public static async Task<(string FirstItem, string[] Trace)> CancelAfterFirstItemAndTrace(
        Func<CancellationToken, IAsyncEnumerable<string>> createSource,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var enumerator = createSource(cts.Token).GetAsyncEnumerator(cts.Token);
        var hasItem = await enumerator.MoveNextAsync();
        hasItem.ShouldBeTrue();
        var firstItem = enumerator.Current;
        await cts.CancelAsync();

        async Task Act()
        {
            await enumerator.MoveNextAsync();
        }

        await ((Func<Task>)Act).ShouldThrowAssignableTo<OperationCanceledException>();
        return (firstItem, readTrace());
    }

    /// <summary>Reads the first item, asserts that the next MoveNextAsync throws InvalidOperationException with message 'boom', and returns the first item with the trace.</summary>
    public static async Task<(string FirstItem, string[] Trace)> ThrowAfterFirstItemAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        var hasItem = await enumerator.MoveNextAsync();
        hasItem.ShouldBeTrue();
        var firstItem = enumerator.Current;

        async Task Act()
        {
            await enumerator.MoveNextAsync();
        }

        var exception = await ((Func<Task>)Act).ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("boom");
        return (firstItem, readTrace());
    }

    /// <summary>Reads the first item, disposes the enumerator early, and returns the item with the trace recorded afterwards.</summary>
    public static async Task<(string FirstItem, string[] Trace)> ReadFirstItemThenDisposeAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace,
        CancellationToken ct)
    {
        string firstItem;

        await using (var enumerator = source.GetAsyncEnumerator(ct))
        {
            var hasItem = await enumerator.MoveNextAsync();
            hasItem.ShouldBeTrue();
            firstItem = enumerator.Current;
        }

        return (firstItem, readTrace());
    }

    /// <summary>Enumerates the whole stream and returns all items with the trace recorded afterwards.</summary>
    public static async Task<(string[] Items, string[] Trace)> CollectItemsAndTrace(
        IAsyncEnumerable<string> source,
        Func<string[]> readTrace)
    {
        var items = await AsyncEnumerableTestHelper.Collect(source);
        return (items, readTrace());
    }
}
