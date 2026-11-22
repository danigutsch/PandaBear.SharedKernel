using PandaBear.BuildingBlocks;

namespace PandaBear.Galleries;

/// <summary>The application's rules for its albums: the cultures they are written in, the most photos an album holds, and text lengths.</summary>
public sealed class AlbumRules
{
    /// <summary>The default <see cref="MaxTitleLength"/>.</summary>
    public const int DefaultMaxTitleLength = 150;

    private readonly int _maxTitleLength = DefaultMaxTitleLength;
    private readonly int _maxSlugLength = Slug.DefaultMaxLength;

    /// <summary>Initializes the rules.</summary>
    /// <param name="cultures">The cultures albums may be written in; a translation in the default culture is required.</param>
    /// <param name="maxPhotos">The most photos an album holds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cultures"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPhotos"/> is zero or negative.</exception>
    public AlbumRules(SupportedCultures cultures, int maxPhotos)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPhotos);

        Cultures = cultures;
        MaxPhotos = maxPhotos;
    }

    /// <summary>Gets the cultures albums may be written in.</summary>
    public SupportedCultures Cultures { get; }

    /// <summary>Gets the most photos an album holds.</summary>
    public int MaxPhotos { get; }

    /// <summary>Gets the longest title, after sanitizing. The default is <see cref="DefaultMaxTitleLength"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxTitleLength
    {
        get => _maxTitleLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxTitleLength = value;
        }
    }

    /// <summary>Gets the longest slug. The default is <see cref="Slug.DefaultMaxLength"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxSlugLength
    {
        get => _maxSlugLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxSlugLength = value;
        }
    }
}
