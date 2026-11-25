using SharedKernel.ContentRights;

namespace SharedKernel.Blazor.Site;

/// <summary>One approved credit and its presentation in a photo gallery or credits page.</summary>
/// <param name="Id">The unique fragment identifier for the photo on its credits page.</param>
/// <param name="Title">The caller's photo label for headings and link context.</param>
/// <param name="Credit">The reviewed public credit.</param>
public sealed record PhotoCreditItem(string Id, string Title, PublicContentCredit Credit)
{
    /// <summary>Gets the optional localized label of the photo's terms, such as CC BY 4.0.</summary>
    public string? TermsLabel { get; init; }

    /// <summary>Gets the optional address of this photo's complete credit, including its fragment if needed.</summary>
    public LinkTarget? DetailsHref { get; init; }

    /// <summary>Checks that the credit can be addressed and described in the rendered page.</summary>
    /// <param name="parameterName">The component parameter that supplied the item.</param>
    /// <exception cref="ArgumentException">The id or title is missing, or the id contains white space or a control character.</exception>
    /// <exception cref="ArgumentNullException">The public credit is missing.</exception>
    internal void Require(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("A photo credit id must be present and contain no white space or control characters.", parameterName);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Title, parameterName);
        ArgumentNullException.ThrowIfNull(Credit, parameterName);
    }
}
