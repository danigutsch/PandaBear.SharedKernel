namespace SharedKernel.Blazor.Site;

/// <summary>
/// Where content lies along one axis of its box: the block axis for <see cref="Split.Alignment" /> (top or middle) and the inline axis
/// for <see cref="Section.TextAlignment" />, <see cref="Band.TextAlignment" />, <see cref="Card.TextAlignment" /> and
/// <see cref="SiteFooter.TextAlignment" /> (the reading start or the middle), and for <see cref="CardGrid.ItemAlignment" /> (a card at
/// the start or in the middle of its column). On a section or band the alignment is nullable: <see langword="null" /> inherits the
/// nearest aligned ancestor, and an explicit value overrides it. A card does not inherit: it is at the start unless its own alignment
/// centres it.
/// </summary>
public enum SiteAlignment
{
    /// <summary>At the start: the top of the block axis, or the side where reading starts on the inline axis.</summary>
    Start = 0,

    /// <summary>In the middle.</summary>
    Center = 1,
}
