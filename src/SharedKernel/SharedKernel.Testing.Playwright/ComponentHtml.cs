using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace SharedKernel.Testing.Playwright;

/// <summary>Renders a Razor component to HTML as static server-side rendering does, for a browser test to open without a host.</summary>
public static class ComponentHtml
{
    /// <summary>Renders <typeparamref name="TComponent"/> with <paramref name="parameters"/> and an empty service provider.</summary>
    /// <typeparam name="TComponent">The component to render.</typeparam>
    /// <param name="parameters">The component parameters by name, or <see langword="null"/> for none.</param>
    /// <returns>The rendered HTML.</returns>
    public static async Task<string> Render<TComponent>(IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
    {
        var services = new ServiceCollection().BuildServiceProvider();
        await using (services.ConfigureAwait(false))
        {
            var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
            await using (renderer.ConfigureAwait(false))
            {
                return await renderer.Dispatcher.InvokeAsync(async () =>
                {
                    var output = await renderer.RenderComponentAsync<TComponent>(
                        parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters)).ConfigureAwait(true);
                    return output.ToHtmlString();
                }).ConfigureAwait(false);
            }
        }
    }
}
