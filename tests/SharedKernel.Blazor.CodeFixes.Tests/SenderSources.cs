namespace PandaBear.Blazor.CodeFixes.Tests;

/// <summary>Component sources that inject <c>ISender</c> for the SKBLZ002 code fix tests.</summary>
internal static class SenderSources
{
    /// <summary>Builds a component that injects <c>ISender</c> and runs <paramref name="use"/> in a method.</summary>
    /// <param name="use">The statements placed in the component's <c>Run</c> method.</param>
    /// <returns>The component source.</returns>
    public static string Component(string use) => $$"""
        using Microsoft.AspNetCore.Components;
        using PandaBear.Mediator;

        namespace Demo;

        public sealed record Ping : IRequest<int>;

        public sealed record Feed : IStreamRequest<int>;

        public sealed class Page : ComponentBase
        {
            [Inject]
            public ISender Sender { get; set; } = default!;

            private static void Keep(object value) { }

            public async Task Run()
            {
                {{use}}
                await Task.CompletedTask;
            }
        }
        """;
}
