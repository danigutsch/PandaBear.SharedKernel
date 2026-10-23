namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Website address attached to a login.</summary>
/// <param name="Value">
/// Address as stored by the source vault. Not always a valid absolute URI: apps, bare hosts, and regular
/// expressions are allowed.
/// </param>
/// <param name="Match">Autofill match detection, or <see langword="null"/> for the vault default.</param>
internal sealed record LoginUri(string Value, UriMatch? Match = null);
