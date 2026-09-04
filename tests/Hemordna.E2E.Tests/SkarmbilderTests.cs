using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Captures the screens as images so the design can be reviewed against docs/DESIGN.md.
/// Run with HEMORDNA_SCREENSHOT_DIR set to choose where they land.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class SkarmbilderTests
{
    private readonly HemordnaAppFixture _app;

    public SkarmbilderTests(HemordnaAppFixture app) => _app = app;

    private static string OutputDirectory
    {
        get
        {
            var directory = Environment.GetEnvironmentVariable("HEMORDNA_SCREENSHOT_DIR")
                ?? Path.Combine(Path.GetTempPath(), "hemordna-skarmbilder");

            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    [Fact]
    public async Task Capture_the_main_screens()
    {
        var page = await _app.NewPageAsync();

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hemordna" }).WaitForAsync();
        await ShootAsync(page, "01-logga-in");

        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Anna");
        await page.GetByLabel("E-post").FillAsync($"shot-{Guid.NewGuid():N}@example.com");
        await page.GetByLabel("Lösenord").FillAsync("Hemordna-E2E-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Välkommen!" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await ShootAsync(page, "02-skapa-hushall");

        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Andersson");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Anna!" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await ShootAsync(page, "03-min-dag");

        // Mobile: the navigation must become a bottom bar.
        await page.SetViewportSizeAsync(390, 844);
        await ShootAsync(page, "04-min-dag-mobil");
    }

    private static async Task ShootAsync(IPage page, string name)
        => await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(OutputDirectory, $"{name}.png"),
            FullPage = true
        });
}
