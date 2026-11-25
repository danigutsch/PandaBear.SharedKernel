namespace SharedKernel.BuildingBlocks;

/// <summary>Whether published content, such as an album, a page or a tour, or one of its translations, is shown publicly.</summary>
public enum PublicationStatus
{
    /// <summary>Being written; never shown publicly.</summary>
    Draft = 0,

    /// <summary>Shown publicly.</summary>
    Published = 1,
}
