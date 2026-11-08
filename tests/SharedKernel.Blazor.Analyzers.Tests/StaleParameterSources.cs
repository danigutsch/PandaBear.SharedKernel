namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Builds component sources with a parameter read in a lifecycle method for the stale-parameter analyzer tests.</summary>
internal static class StaleParameterSources
{
    /// <summary>Builds a component with the given class attributes, one parameter declaration and a body for the lifecycle override.</summary>
    /// <param name="classAttributes">The attribute lines placed on the class.</param>
    /// <param name="members">The property declarations.</param>
    /// <param name="lifecycle">The lifecycle method declaration.</param>
    /// <returns>The C# source.</returns>
    public static string Page(string classAttributes, string members, string lifecycle) => $$"""
        namespace Demo;

        {{classAttributes}}
        public sealed class Page : ComponentBase
        {
            {{members}}

            public string? Name { get; set; }

            {{lifecycle}}
        }
        """;

    /// <summary>Builds an <c>OnInitializedAsync</c> override with the given statement.</summary>
    /// <param name="statement">The statement in the body.</param>
    /// <returns>The method declaration.</returns>
    public static string OnInitializedAsync(string statement) => $$"""
        protected override Task OnInitializedAsync()
        {
            {{statement}}
            return Task.CompletedTask;
        }
        """;

    /// <summary>Builds an <c>OnInitialized</c> override with the given statement.</summary>
    /// <param name="statement">The statement in the body.</param>
    /// <returns>The method declaration.</returns>
    public static string OnInitialized(string statement) => $$"""
        protected override void OnInitialized()
        {
            {{statement}}
        }
        """;

    /// <summary>Builds an <c>OnParametersSetAsync</c> override with the given statement.</summary>
    /// <param name="statement">The statement in the body.</param>
    /// <returns>The method declaration.</returns>
    public static string OnParametersSetAsync(string statement) => $$"""
        protected override Task OnParametersSetAsync()
        {
            {{statement}}
            return Task.CompletedTask;
        }
        """;
}
