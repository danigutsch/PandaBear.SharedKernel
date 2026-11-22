namespace PandaBear.Galleries;

/// <summary>Orders albums for display.</summary>
public static class AlbumOrdering
{
    /// <summary>
    /// Orders albums by their sort order, lower first, and breaks ties by the album id, so the order is the same on every request. A
    /// database query orders the same way: <c>OrderBy(a =&gt; a.SortOrder).ThenBy(a =&gt; a.Id)</c>.
    /// </summary>
    /// <typeparam name="TAlbum">The album type of the application.</typeparam>
    /// <typeparam name="TAlbumId">The album id type.</typeparam>
    /// <param name="albums">The albums.</param>
    /// <param name="sortOrder">Reads an album's sort order.</param>
    /// <param name="id">Reads an album's id.</param>
    /// <returns>The ordered albums.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IOrderedEnumerable<TAlbum> InDisplayOrder<TAlbum, TAlbumId>(IEnumerable<TAlbum> albums, Func<TAlbum, int> sortOrder, Func<TAlbum, TAlbumId> id)
        where TAlbumId : IComparable<TAlbumId>
    {
        ArgumentNullException.ThrowIfNull(albums);
        ArgumentNullException.ThrowIfNull(sortOrder);
        ArgumentNullException.ThrowIfNull(id);

        return albums.OrderBy(sortOrder).ThenBy(id);
    }
}
