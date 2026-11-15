using Microsoft.AspNetCore.Components;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// Contact data: an optional heading, and an <c>address</c> (only when there is something to show) with postal lines and <c>tel:</c>, <c>mailto:</c> and <c>https://wa.me/</c>
/// links built from validated <see cref="PhoneNumber" /> and <see cref="EmailAddress" /> values. A value restored without
/// validation is checked again, so only digits reach a <c>tel:</c> or WhatsApp link and no query reaches a <c>mailto:</c> link.
/// </summary>
public sealed partial class ContactCard
{
    private const string WhatsAppBase = "https://wa.me/";

    /// <summary>Gets or sets the heading text. Without it no heading is rendered.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 3.</summary>
    [Parameter]
    public int HeadingLevel { get; set; } = 3;

    /// <summary>Gets or sets the postal address, one entry per line.</summary>
    [Parameter]
    public IReadOnlyList<string>? AddressLines { get; set; }

    /// <summary>Gets or sets the phone number, linked with <c>tel:</c>.</summary>
    [Parameter]
    public PhoneNumber? Phone { get; set; }

    /// <summary>
    /// Gets or sets the visible text of the phone link, for example <c>+55 (11) 99999-0000</c>. It must be the same number as
    /// <see cref="Phone" />; separators are allowed. Without it the canonical digits are shown.
    /// </summary>
    [Parameter]
    public string? PhoneText { get; set; }

    /// <summary>Gets or sets the email address, linked with <c>mailto:</c>.</summary>
    [Parameter]
    public EmailAddress? Email { get; set; }

    /// <summary>Gets or sets the WhatsApp number, linked with <c>https://wa.me/</c>.</summary>
    [Parameter]
    public PhoneNumber? WhatsApp { get; set; }

    /// <summary>Gets or sets the WhatsApp link text. Caller-supplied so it can be localized.</summary>
    [Parameter]
    public string WhatsAppLabel { get; set; } = "WhatsApp";

    private bool HasLines => AddressLines is { Count: > 0 };

    private bool HasChannels => Phone is not null || Email is not null || WhatsApp is not null;

    private string? PhoneHref => Phone is null ? null : "tel:" + Phone.Value;

    private string? EmailHref => Email is null ? null : "mailto:" + Email.Value;

    private string? WhatsAppHref => WhatsApp is null ? null : WhatsAppBase + WhatsApp.Value.TrimStart('+');

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Heading.RequireLevel(HeadingLevel, nameof(HeadingLevel));
        RequireDigits(Phone, nameof(Phone));
        RequireDigits(WhatsApp, nameof(WhatsApp));
        if (PhoneText is not null && (Phone is null || PhoneNumber.Create(PhoneText) is not { IsSuccess: true } text || text.Value != Phone))
        {
            throw new ArgumentException("The phone text must show the same number as Phone.", nameof(PhoneText));
        }

        if (Email is not null && !Email.Value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '@' or '_' or '+' or '-'))
        {
            throw new ArgumentException("The email address has characters that are not safe in a mailto link.", nameof(Email));
        }
    }

    private static void RequireDigits(PhoneNumber? number, string parameterName)
    {
        if (number is null)
        {
            return;
        }

        var digits = number.Value.StartsWith('+', StringComparison.Ordinal) ? number.Value[1..] : number.Value;
        if (digits.Length is < PhoneNumber.MinDigits or > PhoneNumber.MaxDigits || !digits.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("The phone number is not a validated number of digits.", parameterName);
        }
    }
}
