namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Helpers for consuming asynchronous sequences in generator runtime tests.</summary>
internal static class AsyncEnumerableTestHelper
{
    /// <summary>Awaits the whole async sequence and returns its items in enumeration order as an array.</summary>
    public static async Task<T[]> Collect<T>(IAsyncEnumerable<T> source)
    {
        List<T> items = [];

        await foreach (var item in source)
        {
            items.Add(item);
        }

        return [.. items];
    }
}
