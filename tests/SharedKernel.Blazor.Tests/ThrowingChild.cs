using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>A component that fails while initializing when asked to.</summary>
internal sealed class ThrowingChild : ComponentBase
{
    /// <summary>The message that must never reach the page.</summary>
    public const string SecretMessage = "secret-connection-string";

    /// <summary>Gets or sets whether initialization throws.</summary>
    [Parameter]
    public bool Throw { get; set; } = true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Throw)
        {
            throw new InvalidOperationException(SecretMessage + HostileText.MultilineStackTrace);
        }
    }
}
