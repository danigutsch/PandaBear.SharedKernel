using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace PandaBear.Blazor.Tests.Browser;

/// <summary>
/// The statically rendered document of the picker's browser tests: it reads how many images are chosen from the <c>chosen</c> query
/// value and renders the interactive <see cref="PickerPage"/> with that number. It is the root component and the only page, at <c>/</c>.
/// </summary>
[Route("/")]
public sealed class PickerDocument : ComponentBase
{
    /// <summary>Gets or sets the navigation manager, which gives the request's address.</summary>
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var query = new Uri(Navigation.Uri).Query;
        var chosen = query.StartsWith("?chosen=", StringComparison.Ordinal) && int.TryParse(query["?chosen=".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;
        builder.AddMarkupContent(0, "<!DOCTYPE html>");
        builder.OpenElement(1, "html");
        builder.AddAttribute(2, "lang", "en");
        builder.AddMarkupContent(3, "<head><meta charset=\"utf-8\"><title>Image picker</title><base href=\"/\"></head>");
        builder.OpenElement(4, "body");
        builder.OpenComponent<PickerPage>(5);
        builder.AddComponentParameter(6, nameof(PickerPage.Chosen), chosen);
        builder.AddComponentRenderMode(RenderMode.InteractiveServer);
        builder.CloseComponent();
        builder.AddMarkupContent(7, "<script src=\"_framework/blazor.web.js\"></script>");
        builder.CloseElement();
        builder.CloseElement();
    }
}
