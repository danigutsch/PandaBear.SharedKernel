using Microsoft.Playwright;
using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>Tests choosing a file in a Blazor InputFile whose change listener is attached late, in Chromium; the browser tests skip when the browser is not installed.</summary>
public sealed class PlaywrightInputFileTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    // Stands in for Blazor's InputFile.init, which the server calls after the page is already interactive: 300 ms after load it marks the
    // element the way Blazor does and starts listening for changes, recording the names it is handed.
    private const string LateInputFile = """
        <input type="file" id="upload" multiple>
        <script>
            window.received = [];
            setTimeout(() => {
                const input = document.getElementById('upload');
                input._blazorInputFileNextFileId = 0;
                input.addEventListener('change', () => window.received.push(...Array.from(input.files, file => file.name)));
            }, 300);
        </script>
        """;

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_file_chosen_before_the_listener_is_attached_still_reaches_it()
    {
        // Arrange
        var page = await chromium.Show(LateInputFile);
        var file = new FilePayload { Name = "site.json", MimeType = "application/json", Buffer = "{}"u8.ToArray() };

        // Act
        await page.Locator("#upload").SetBlazorInputFile(file);

        // Assert
        var received = await page.EvaluateAsync<string[]>("() => window.received");
        received.ShouldBe(["site.json"]);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_batch_keeps_its_file_order_after_the_listener_is_attached()
    {
        // Arrange
        var page = await chromium.Show(LateInputFile);
        FilePayload[] files =
        [
            new() { Name = "front.txt", MimeType = "text/plain", Buffer = "front"u8.ToArray() },
            new() { Name = "back.txt", MimeType = "text/plain", Buffer = "back"u8.ToArray() },
        ];

        // Act
        await page.Locator("#upload").SetBlazorInputFiles(files);

        // Assert
        var received = await page.EvaluateAsync<string[]>("() => window.received");
        received.ShouldBe(["front.txt", "back.txt"]);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Waiting_for_the_listener_preserves_a_native_file_chooser_flow()
    {
        // Arrange
        var page = await chromium.Show(LateInputFile);
        var input = page.Locator("#upload");
        var file = new FilePayload { Name = "inventory.csv", MimeType = "text/csv", Buffer = "room"u8.ToArray() };

        // Act
        await input.WaitForBlazorInputFile();
        var chooser = await page.RunAndWaitForFileChooserAsync(() => input.ClickAsync());
        await chooser.SetFilesAsync(file);

        // Assert
        var received = await page.EvaluateAsync<string[]>("() => window.received");
        received.ShouldBe(["inventory.csv"]);
    }

    private const string ReplacedInputFile = """
        <input type="file" id="upload">
        <script>
            window.received = [];
            setTimeout(() => {
                const replacement = document.createElement('input');
                replacement.type = 'file';
                replacement.id = 'upload';
                document.getElementById('upload').replaceWith(replacement);
            }, 100);
            setTimeout(() => {
                const input = document.getElementById('upload');
                input._blazorInputFileNextFileId = 0;
                input.addEventListener('change', () => window.received.push(...Array.from(input.files, file => file.name)));
            }, 300);
        </script>
        """;

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_input_replaced_before_it_is_set_up_is_found_again()
    {
        // Arrange
        var page = await chromium.Show(ReplacedInputFile);
        page.SetDefaultTimeout(3000);
        var file = new FilePayload { Name = "site.json", MimeType = "application/json", Buffer = "{}"u8.ToArray() };

        // Act
        await page.Locator("#upload").SetBlazorInputFile(file);

        // Assert
        var received = await page.EvaluateAsync<string[]>("() => window.received");
        received.ShouldBe(["site.json"]);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Missing_arguments_are_refused()
    {
        // Arrange
        var page = await chromium.Show(LateInputFile);
        var file = new FilePayload { Name = "site.json", MimeType = "application/json", Buffer = [] };
        Func<Task> withoutInput = () => ((ILocator)null!).SetBlazorInputFile(file);
        Func<Task> withoutFile = () => page.Locator("#upload").SetBlazorInputFile(null!);

        // Act
        var missingInput = await withoutInput.ShouldThrow<ArgumentNullException>();
        var missingFile = await withoutFile.ShouldThrow<ArgumentNullException>();

        // Assert
        missingInput.ParamName.ShouldBe("input");
        missingFile.ParamName.ShouldBe("file");
    }
}
