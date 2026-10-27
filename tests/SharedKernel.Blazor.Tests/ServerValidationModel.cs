namespace PandaBear.Blazor.Tests;

/// <summary>A form model for server validation tests.</summary>
internal sealed class ServerValidationModel
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the email address.</summary>
    public string Email { get; set; } = string.Empty;
}
