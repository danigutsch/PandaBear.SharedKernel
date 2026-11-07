namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Builds component sources around a method body for the busy-flag analyzer tests.</summary>
internal static class BusyFlagSources
{
    /// <summary>Builds a component with a <c>_saving</c> flag and a <c>Submit</c> method that runs the given statements.</summary>
    /// <param name="body">The statements of the async <c>Submit</c> method.</param>
    /// <returns>The C# source.</returns>
    public static string Page(string body) => $$"""
        namespace Demo;

        public sealed class Page : ComponentBase
        {
            private bool _saving;

            private Task Save() => Task.CompletedTask;
            private void Log() { }
            private string? Name() => null;
            private async IAsyncEnumerable<int> Numbers() { await Task.CompletedTask; yield return 1; }
            private async IAsyncEnumerable<(int, int)> Pairs() { await Task.CompletedTask; yield return (1, 2); }
            private sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            public async Task Submit()
            {
        {{Indent(body)}}
            }
        }
        """;

    private static string Indent(string body) =>
        string.Join('\n', body.Split('\n').Select(static line => "        " + line));
}
