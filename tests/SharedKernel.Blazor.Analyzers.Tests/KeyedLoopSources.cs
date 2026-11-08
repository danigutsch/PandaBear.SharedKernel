namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Builds component sources with a single loop for the keyed-loop analyzer tests.</summary>
internal static class KeyedLoopSources
{
    /// <summary>Builds a component whose render method loops over a list and runs the given statements for every item.</summary>
    /// <param name="open">The statement that opens the rendered node.</param>
    /// <param name="close">The statements that follow it.</param>
    /// <returns>The C# source.</returns>
    public static string Page(string open, string close) => $$"""
        using Microsoft.AspNetCore.Components.Rendering;

        namespace Demo;

        public sealed class Row : ComponentBase;

        public sealed class Page : ComponentBase
        {
            private readonly List<int> _items = [];

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                foreach (var item in _items)
                {
                    {{open}}
                    {{close}}
                }
            }
        }
        """;
}
