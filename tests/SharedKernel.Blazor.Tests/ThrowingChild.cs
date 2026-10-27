using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>A component that fails while rendering.</summary>
internal sealed class ThrowingChild : ComponentBase
{
    /// <summary>The message that must never reach the page.</summary>
    public const string SecretMessage = "secret-connection-string";

    /// <summary>Gets or sets whether initialization throws.</summary>
    public static bool ShouldThrow { get; set; } = true;

    protected override void OnInitialized()
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException(SecretMessage + HostileText.MultilineStackTrace);
        }
    }
}
