namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The application's composite formats for the full credit of an image, as a credits page lists it (see
/// <see cref="ImageCredit.ToFullText"/>). <paramref name="WithTitle"/> and <paramref name="WithoutTitle"/> take the placeholders of
/// <see cref="ImageCredit.ToShortText"/>: <c>{0}</c> the author, <c>{1}</c> the licence code, <c>{2}</c> the title and <c>{3}</c> the
/// source address (empty without one). In
/// <paramref name="Modified"/>, <c>{0}</c> is the text so far, such as <c>{0} (adaptada)</c>.
/// </summary>
/// <param name="WithTitle">The format of a credit with a title, such as <c>“{2}”, de {0}, licença {1}</c>.</param>
/// <param name="WithoutTitle">The format of a credit without a title, such as <c>Foto de {0}, licença {1}</c>.</param>
/// <param name="Modified">The format that notes that the image was changed, such as cropped or resized.</param>
public sealed record ImageCreditFormats(string WithTitle, string WithoutTitle, string Modified)
{
    /// <summary>Gets the format of a credit with a title.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string WithTitle { get; } = Required(WithTitle, nameof(WithTitle));

    /// <summary>Gets the format of a credit without a title.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string WithoutTitle { get; } = Required(WithoutTitle, nameof(WithoutTitle));

    /// <summary>Gets the format that notes a change.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string Modified { get; } = Required(Modified, nameof(Modified));

    private static string Required(string format, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format, name);
        return format;
    }
}
