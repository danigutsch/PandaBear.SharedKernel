using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>What a catalog shows and withholds, for the summary of the <c>catalog</c> command.</summary>
internal sealed class CatalogCounts
{
    /// <summary>Items whose notes are shown.</summary>
    public int SharedNotes { get; private set; }

    /// <summary>Items whose notes are withheld until the user decides.</summary>
    public int PendingNotes { get; private set; }

    /// <summary>Items whose notes stay private.</summary>
    public int PrivateNotes { get; private set; }

    /// <summary>Items whose notes <c>apply</c> moves into a hidden field.</summary>
    public int HiddenNotes { get; private set; }

    /// <summary>Custom fields the catalog does not show.</summary>
    public int ProtectedFields { get; set; }

    /// <summary>Website addresses the catalog masks.</summary>
    public int MaskedUris { get; set; }

    /// <summary>Counts the notes of one item.</summary>
    /// <param name="decision">The decision on its notes.</param>
    public void Count(NotesDecision decision)
    {
        switch (decision)
        {
            case NotesDecision.Share:
                SharedNotes++;
                break;
            case NotesDecision.Private:
                PrivateNotes++;
                break;
            case NotesDecision.Hidden:
                HiddenNotes++;
                break;
            default:
                PendingNotes++;
                break;
        }
    }
}
