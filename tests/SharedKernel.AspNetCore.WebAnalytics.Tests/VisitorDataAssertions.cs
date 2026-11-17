using PandaBear.Testing.Logging;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>Asserts that a log entry holds none of the visitor data the test pages carry.</summary>
internal static class VisitorDataAssertions
{
    /// <summary>The path the test pages are requested on.</summary>
    internal const string SecretPath = "/pt/secret-path";

    /// <summary>The host of the referrer the test pages are requested with.</summary>
    internal const string SecretReferrerHost = "private.example.org";

    /// <summary>
    /// Fails when the message, the state values or the exception of <paramref name="record"/> contain the visitor's address (full or
    /// shortened), browser, path or referrer.
    /// </summary>
    /// <param name="record">The log entry.</param>
    internal static void ShouldHoldNoVisitorData(this RecordedLog record)
    {
        var text = record.Message + string.Join(' ', record.State.Select(pair => pair.Value)) + record.Exception;
        text.ShouldNotContain(PageContexts.VisitorAddress.ToString(), StringComparison.Ordinal);
        text.ShouldNotContain(PageContexts.ShortenedVisitorAddress.ToString(), StringComparison.Ordinal);
        text.ShouldNotContain("Firefox", StringComparison.Ordinal);
        text.ShouldNotContain(SecretPath, StringComparison.Ordinal);
        text.ShouldNotContain(SecretReferrerHost, StringComparison.Ordinal);
    }
}
