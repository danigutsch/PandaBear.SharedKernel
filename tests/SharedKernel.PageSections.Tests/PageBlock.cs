namespace SharedKernel.PageSections.Tests;

/// <summary>V2's settings block of a section.</summary>
public enum PageBlock
{
    /// <summary>No block.</summary>
    None,

    /// <summary>The contact details.</summary>
    ContactDetails,

    /// <summary>The bank details, which need the settings to publish.</summary>
    BankDetails,
}
