using Microsoft.Playwright;
using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>Exercises an Equal Access browser bundle in Chromium.</summary>
/// <param name="chromium">The isolated browser fixture.</param>
public sealed class PlaywrightEqualAccessTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_deduplicated_failure_reports_only_definite_violation_rule_IDs()
    {
        // Arrange
        using var bundle = new EqualAccessTestBundle("""
            var ace = { Checker: class {
                check(root, policies) {
                    if (root !== document || policies.join(',') !== 'WCAG_2_2') throw Error('Incorrect audit target');
                    return { results: [
                        { ruleId: 'z_document', value: ['VIOLATION', 'FAIL'], snippet: '<input value="private-marker">' },
                        { ruleId: 'a_image', value: ['VIOLATION', 'FAIL'], message: document.body.textContent },
                        { ruleId: 'a_image', value: ['VIOLATION', 'FAIL'] },
                        { ruleId: 'manual', value: ['VIOLATION', 'MANUAL'] },
                        { ruleId: 'recommended', value: ['RECOMMENDATION', 'FAIL'] },
                        { ruleId: 'potential', value: ['VIOLATION', 'POTENTIAL'] }
                    ] };
                }
            } };
            """);
        var page = await chromium.Show("<p>private-marker</p>");
        Func<Task> scan = () => page.ShouldHaveNoEqualAccessViolations(bundle.Path, bundle.Sha256);

        // Act
        var failure = await scan.ShouldThrow<PlaywrightException>();

        // Assert
        failure.Message.ShouldBe("Equal Access detected WCAG 2.2 violations: a_image, z_document.");
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task The_verified_bundle_evaluates_the_document_with_strict_CSP_enforced()
    {
        // Arrange
        using var bundle = new EqualAccessTestBundle("""
            var ace = { Checker: class {
                check(root, policies) {
                    if (!root.querySelector('h1') || policies.join(',') !== 'WCAG_2_2') throw Error('Incorrect audit target');
                    root.documentElement.dataset.auditRan = 'true';
                    return { results: [{ ruleId: 'page_heading', value: ['VIOLATION', 'PASS'] }] };
                }
            } };
            """);
        var page = await chromium.Show(string.Empty);
        await page.SetContentAsync("""
            <!doctype html><html lang="en"><head>
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'">
            <title>Test page</title></head><body><h1>Test page</h1></body></html>
            """);

        // Act
        await page.ShouldHaveNoEqualAccessViolations(bundle.Path, bundle.Sha256.ToUpperInvariant());
        var engineRan = await page.EvaluateAsync<bool>("() => document.documentElement.dataset.auditRan === 'true'");
        var inlineRan = await page.EvaluateAsync<bool>("""
            () => {
                const script = document.createElement('script');
                script.textContent = 'globalThis.inlineRan = true';
                document.head.appendChild(script).remove();
                return globalThis.inlineRan === true;
            }
            """);

        // Assert
        engineRan.ShouldBeTrue();
        inlineRan.ShouldBeFalse();
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_mismatched_pin_fails_before_the_script_executes()
    {
        // Arrange
        using var bundle = new EqualAccessTestBundle("globalThis.bundleRan = true;");
        var page = await chromium.Show("<h1>Page</h1>");
        Func<Task> scan = () => page.ShouldHaveNoEqualAccessViolations(bundle.Path, new string('a', 64));

        // Act
        var failure = await scan.ShouldThrow<InvalidDataException>();
        var scriptRan = await page.EvaluateAsync<bool>("() => globalThis.bundleRan === true");

        // Assert
        failure.Message.ShouldBe("The local Equal Access bundle does not match the expected SHA-256 digest.");
        scriptRan.ShouldBeFalse();
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData("0123")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task An_invalid_pin_is_rejected_as_an_argument(string expectedSha256)
    {
        // Arrange
        using var bundle = new EqualAccessTestBundle("globalThis.bundleRan = true;");
        var page = await chromium.Show("<h1>Page</h1>");
        Func<Task> scan = () => page.ShouldHaveNoEqualAccessViolations(bundle.Path, expectedSha256);

        // Act
        var failure = await scan.ShouldThrow<ArgumentException>();

        // Assert
        failure.ParamName.ShouldBe(nameof(expectedSha256));
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData("return null;")]
    [InlineData("return { results: [] };")]
    [InlineData("return { results: [{ ruleId: 'private-marker secret', value: ['VIOLATION', 'FAIL'] }] };")]
    [InlineData("return { results: [{ ruleId: 'a_rule', value: ['VIOLATION', 'private-marker'] }] };")]
    [InlineData("return { results: [{ ruleId: 'a_rule', value: 'VIOLATION' }] };")]
    public async Task Unusable_engine_reports_fail_closed_without_returning_raw_values(string body)
    {
        // Arrange
        using var bundle = new EqualAccessTestBundle($"var ace = {{ Checker: class {{ check() {{ {body} }} }} }};");
        var page = await chromium.Show("<h1>Page</h1>");
        Func<Task> scan = () => page.ShouldHaveNoEqualAccessViolations(bundle.Path, bundle.Sha256);

        // Act
        var failure = await scan.ShouldThrow<PlaywrightException>();

        // Assert
        failure.Message.ShouldBe("Equal Access returned an invalid WCAG 2.2 report.");
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Browser_errors_are_reported_without_remote_exception_content()
    {
        // Arrange
        using var bundle = new EqualAccessTestBundle("""
            var ace = { Checker: class { check() { throw Error(document.body.textContent); } } };
            """);
        var page = await chromium.Show("<p>private-marker</p>");
        Func<Task> scan = () => page.ShouldHaveNoEqualAccessViolations(bundle.Path, bundle.Sha256, "Gallery preview.");

        // Act
        var failure = await scan.ShouldThrow<PlaywrightException>();

        // Assert
        failure.Message.ShouldBe("Gallery preview. The Equal Access WCAG 2.2 scan could not complete.");
        failure.InnerException.ShouldBeNull();
    }
}
