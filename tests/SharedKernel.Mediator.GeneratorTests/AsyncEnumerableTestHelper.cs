namespace SharedKernel.Mediator.GeneratorTests;

internal static class AsyncEnumerableTestHelper
{
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
