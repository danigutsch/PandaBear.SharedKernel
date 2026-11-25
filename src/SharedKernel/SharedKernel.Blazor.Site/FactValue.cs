namespace SharedKernel.Blazor.Site;

/// <summary>One value of a <see cref="FactCard" />: its text and, optionally, the address it links to.</summary>
/// <param name="Text">The text, for example "8 days" or "info@example.org". It must not be blank.</param>
/// <param name="Link">
/// The optional link target, such as a <c>mailto:</c>, <c>tel:</c> or <c>https</c> address. With it the text is a link in the same tab;
/// without it the text is plain.
/// </param>
public sealed record FactValue(string Text, LinkTarget? Link = null);
