namespace CommunityVenue;

/// <summary>A published translation of a venue page.</summary>
internal sealed record VenuePage(
    string Key,
    string Language,
    string Slug,
    string Title,
    string Intro,
    string Heading,
    string Body,
    DateTimeOffset Modified)
{
    /// <summary>Gets the public address of the translation.</summary>
    internal string Path => $"/{Language}/{Slug}";
}
