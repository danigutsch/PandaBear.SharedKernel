using SharedKernel.BuildingBlocks;

namespace SharedKernel.Galleries;

/// <summary>Unvalidated text of an album in one culture.</summary>
/// <param name="Culture">The culture name, one of <see cref="AlbumRules.Cultures"/>.</param>
/// <param name="Title">The title.</param>
/// <param name="Slug">The slug of the album's address; blank derives it from the title.</param>
/// <param name="Status">Whether the translation is published.</param>
public sealed record AlbumTranslationDefinition(string? Culture, string? Title, string? Slug, PublicationStatus Status);
