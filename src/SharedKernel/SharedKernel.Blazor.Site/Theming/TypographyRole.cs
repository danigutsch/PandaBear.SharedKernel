namespace SharedKernel.Blazor.Site.Theming;

/// <summary>A typography role of the site theme; each maps to seven <c>--site-{role}-*</c> custom properties.</summary>
public enum TypographyRole
{
    /// <summary>Level 1 headings (<c>h1</c>).</summary>
    H1 = 0,

    /// <summary>Level 2 headings (<c>h2</c>).</summary>
    H2 = 1,

    /// <summary>Level 3 headings (<c>h3</c>).</summary>
    H3 = 2,

    /// <summary>Level 4 headings (<c>h4</c>).</summary>
    H4 = 3,

    /// <summary>Level 5 headings (<c>h5</c>).</summary>
    H5 = 4,

    /// <summary>Level 6 headings (<c>h6</c>).</summary>
    H6 = 5,

    /// <summary>Body text, inherited by paragraphs and lists (<c>body</c>).</summary>
    Body = 6,

    /// <summary>Small print (<c>small</c>, the footer's legal line and image captions).</summary>
    Small = 7,

    /// <summary>Links outside headings (<c>a</c>).</summary>
    Link = 8,

    /// <summary>Links styled as buttons (<c>.site-button</c>).</summary>
    Button = 9,

    /// <summary>Header navigation items (<c>.site-nav a</c> and <c>.site-nav summary</c>).</summary>
    Nav = 10,

    /// <summary>
    /// Card headings (<c>.site-card__title</c>), whatever their level, so a card title does not take the size or the transform of the
    /// section heading of its level.
    /// </summary>
    CardTitle = 11,

    /// <summary>
    /// The site's name as text in the header's brand slot (<c>.site-header__brand a</c>), when there is no logo, so it takes a heading
    /// face instead of the link role and no underline.
    /// </summary>
    Brand = 12,
}
