using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;

namespace PandaBear.Testing.Playwright;

/// <summary>Provides accessibility assertions using an IBM Equal Access browser bundle.</summary>
/// <remarks>Use a reviewed local browser bundle in a test after the state under test is interactive.
/// The assertion checks the current document once with the WCAG 2.2 rule set. It reports only rule IDs for
/// definite violations. It does not change the page's Content Security Policy or install a script element.</remarks>
public static class PlaywrightEqualAccessExtensions
{
    /// <summary>Checks the rendered document for definite WCAG 2.2 violations.</summary>
    /// <param name="page">The interactive page to check.</param>
    /// <param name="engineBundlePath">The local IBM Equal Access browser bundle.</param>
    /// <param name="expectedSha256">The expected SHA-256 digest of the bundle in hexadecimal.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The file path is blank or the digest is not 64 hexadecimal characters.</exception>
    /// <exception cref="InvalidDataException">The bundle does not match the expected digest.</exception>
    /// <exception cref="PlaywrightException">The browser scan fails, gives an invalid report, or finds definite violations.</exception>
    public static Task ShouldHaveNoEqualAccessViolations(this IPage page, string engineBundlePath, string expectedSha256) =>
        Verify(page, engineBundlePath, expectedSha256, null);

    /// <summary>Checks the rendered document for definite WCAG 2.2 violations.</summary>
    /// <param name="page">The interactive page to check.</param>
    /// <param name="engineBundlePath">The local IBM Equal Access browser bundle.</param>
    /// <param name="expectedSha256">The expected SHA-256 digest of the bundle in hexadecimal.</param>
    /// <param name="userMessage">The non-sensitive explanation to prefix to the assertion failure.</param>
    /// <returns>A task that completes when the check passes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The file path is blank or the digest is not 64 hexadecimal characters.</exception>
    /// <exception cref="InvalidDataException">The bundle does not match the expected digest.</exception>
    /// <exception cref="PlaywrightException">The browser scan fails, gives an invalid report, or finds definite violations.</exception>
    public static Task ShouldHaveNoEqualAccessViolations(this IPage page, string engineBundlePath, string expectedSha256, string userMessage)
    {
        ArgumentNullException.ThrowIfNull(userMessage);
        return Verify(page, engineBundlePath, expectedSha256, userMessage);
    }

    private static async Task Verify(IPage page, string engineBundlePath, string expectedSha256, string? userMessage)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineBundlePath);
        ArgumentNullException.ThrowIfNull(expectedSha256);
        if (expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("The SHA-256 digest must contain 64 hexadecimal characters.", nameof(expectedSha256));
        }

        var bytes = await File.ReadAllBytesAsync(engineBundlePath).ConfigureAwait(false);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The local Equal Access bundle does not match the expected SHA-256 digest.");
        }

        string[]? failedRules;
        try
        {
            failedRules = await page.EvaluateAsync<string[]?>(AuditScript(Encoding.UTF8.GetString(bytes))).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            throw new PlaywrightException(WithContext(userMessage, "The Equal Access WCAG 2.2 scan could not complete."));
        }

        if (failedRules is null)
        {
            throw new PlaywrightException(WithContext(userMessage, "Equal Access returned an invalid WCAG 2.2 report."));
        }

        if (failedRules.Length > 0)
        {
            throw new PlaywrightException(WithContext(userMessage, $"Equal Access detected WCAG 2.2 violations: {string.Join(", ", failedRules)}."));
        }
    }

    // Keep the bundle's ace export in this evaluation scope, separate from any page-owned global.
    // Project results in the browser so the report and browser exception details stay on that side.
    private static string AuditScript(string bundle) => $$"""
        async () => {
            try {
                var ace;
                {{bundle}}
                const report = await new ace.Checker().check(document, ['WCAG_2_2']);
                if (!Array.isArray(report?.results) || report.results.length === 0) return null;

                const categories = ['VIOLATION', 'RECOMMENDATION', 'INFORMATION'];
                const outcomes = ['PASS', 'FAIL', 'POTENTIAL', 'MANUAL'];
                const violations = new Set();
                for (const result of report.results) {
                    const value = result?.value;
                    const id = result?.ruleId;
                    if (!Array.isArray(value) || value.length !== 2
                        || !categories.includes(value[0]) || !outcomes.includes(value[1])
                        || typeof id !== 'string' || id.length > 128 || !/^[A-Za-z]/u.test(id)
                        || /[^A-Za-z0-9_]/u.test(id)) return null;

                    if (value[0] === 'VIOLATION' && value[1] === 'FAIL') violations.add(id);
                }

                return [...violations].sort();
            } catch {
                throw new Error('Equal Access scan failed.');
            }
        }
        """;

    private static string WithContext(string? userMessage, string message) => userMessage is null ? message : $"{userMessage} {message}";
}
