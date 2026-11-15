namespace Aspire.Hosting;

/// <summary>What setting up a website in Umami did.</summary>
internal enum UmamiWebsiteOutcome
{
    /// <summary>The website was created with its id.</summary>
    Created,

    /// <summary>A website with the id existed, so nothing was changed.</summary>
    AlreadyExists,
}
