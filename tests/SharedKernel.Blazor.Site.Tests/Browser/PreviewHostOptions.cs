namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>How the browser test host renders the preview.</summary>
/// <param name="RenderNonce">Whether the preview gets the response's nonce; otherwise it gets <see cref="WrongNonce" />, which no policy names.</param>
public sealed record PreviewHostOptions(bool RenderNonce)
{
    /// <summary>A nonce that no response's policy names.</summary>
    public const string WrongNonce = "bm90LXRoZS1ub25jZQ==";
}
