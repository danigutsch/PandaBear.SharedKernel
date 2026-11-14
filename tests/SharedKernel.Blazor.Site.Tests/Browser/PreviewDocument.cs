using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using PandaBear.AspNetCore;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// The statically rendered document of the browser test: it reads the response's nonce and hands it to the interactive
/// <see cref="PreviewSample" /> as a parameter, so the circuit renders the nonce the policy of the page names.
/// It is the root component and the only page, at <c>/</c>.
/// </summary>
[Route("/")]
public sealed class PreviewDocument : ComponentBase
{
    /// <summary>Gets or sets the request's nonce.</summary>
    [Inject]
    public ICspNonce CspNonce
    {
        get; set;
    } = default!;

    /// <summary>Gets or sets the host options.</summary>
    [Inject]
    public PreviewHostOptions Options
    {
        get; set;
    } = default!;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddMarkupContent(0, "<!DOCTYPE html>");
        builder.OpenElement(1, "html");
        builder.AddAttribute(2, "lang", "en");
        builder.AddMarkupContent(3, "<head><meta charset=\"utf-8\"><title>Theme preview</title><base href=\"/\"></head>");
        builder.OpenElement(4, "body");
        builder.OpenComponent<PreviewSample>(5);
        builder.AddComponentParameter(6, nameof(PreviewSample.Nonce), Options.RenderNonce ? CspNonce.Value : PreviewHostOptions.WrongNonce);
        builder.AddComponentRenderMode(RenderMode.InteractiveServer);
        builder.CloseComponent();
        builder.AddMarkupContent(7, "<script src=\"_framework/blazor.web.js\"></script>");
        builder.CloseElement();
        builder.CloseElement();
    }
}
