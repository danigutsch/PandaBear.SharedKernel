namespace PandaBear.Galleries;

/// <summary>Whether a translation of an album is shown publicly.</summary>
public enum PublicationStatus
{
    /// <summary>Being written; never shown publicly.</summary>
    Draft = 0,

    /// <summary>Shown publicly.</summary>
    Published = 1,
}
