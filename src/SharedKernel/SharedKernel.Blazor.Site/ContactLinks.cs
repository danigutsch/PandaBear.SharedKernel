using System.Globalization;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// Builds the <c>tel:</c>, <c>mailto:</c> and WhatsApp (<c>https://wa.me/</c>) links of contact values, so a site's header, footer,
/// contact card and fact grids link a contact the same way. Each link is a <see cref="LinkTarget" /> with a fixed scheme; a value that
/// cannot be written into that scheme safely, such as one restored without validation, gets no link (<see langword="null" />) rather
/// than an escaped or another kind of address.
/// </summary>
public static class ContactLinks
{
    private const string WhatsAppBase = "https://wa.me/";

    /// <summary>Links a phone number with <c>tel:</c> and its canonical digits, with the leading <c>+</c> when it has one.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The link, such as <c>tel:+5511999990000</c>, or <see langword="null" /> when the number is not 7 to 15 digits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="number" /> is <see langword="null" />.</exception>
    public static LinkTarget? Phone(PhoneNumber number)
    {
        ArgumentNullException.ThrowIfNull(number);
        return Digits(number) is null ? null : Link("tel:" + number.Value);
    }

    /// <summary>
    /// Links a WhatsApp number to its chat at <c>https://wa.me/</c> with the digits only, as WhatsApp asks: the full international
    /// number without the <c>+</c>.
    /// </summary>
    /// <param name="number">The number, with its country code.</param>
    /// <returns>The link, such as <c>https://wa.me/5511999990000</c>, or <see langword="null" /> when the number is not 7 to 15 digits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="number" /> is <see langword="null" />.</exception>
    public static LinkTarget? WhatsApp(PhoneNumber number)
    {
        ArgumentNullException.ThrowIfNull(number);
        return Digits(number) is { } digits ? Link(WhatsAppBase + digits) : null;
    }

    /// <summary>
    /// Links an email address with <c>mailto:</c>. The local part is linked only when it holds ASCII letters, digits, <c>.</c>,
    /// <c>_</c>, <c>+</c> and <c>-</c>, which a <c>mailto:</c> address carries as they are; any other character, such as <c>?</c>,
    /// <c>&amp;</c>, <c>%</c>, <c>,</c> or a quote, could start a header or another recipient in a mail program, so such an address gets no
    /// link. An international domain is written in its ASCII (Punycode) form.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>
    /// The link, such as <c>mailto:info@example.org</c>, or <see langword="null" /> when the address cannot be linked safely.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="address" /> is <see langword="null" />.</exception>
    public static LinkTarget? Email(EmailAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var value = address.Value;
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || !value[..at].All(IsLocalCharacter) || AsciiDomain(value[(at + 1)..]) is not { } domain)
        {
            return null;
        }

        return Link("mailto:" + value[..at] + "@" + domain);
    }

    private static string? Digits(PhoneNumber number)
    {
        var value = number.Value;
        var digits = value.StartsWith('+', StringComparison.Ordinal) ? value[1..] : value;
        return digits.Length is >= PhoneNumber.MinDigits and <= PhoneNumber.MaxDigits && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    private static bool IsLocalCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '+' or '-';

    private static string? AsciiDomain(string domain)
    {
        try
        {
            return domain.Length == 0 ? null : new IdnMapping { UseStd3AsciiRules = true }.GetAscii(domain);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static LinkTarget? Link(string address) => LinkTarget.Create(address) is { IsSuccess: true } link ? link.Value : null;
}
