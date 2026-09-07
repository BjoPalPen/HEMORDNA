using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Captures the screens as images so the design can be reviewed against docs/DESIGN.md.
/// Run with HEMORDNA_SCREENSHOT_DIR set to choose where they land.
///
/// The household is seeded with a room from a template (so "Idag"/"Rum" show real tasks, not
/// an empty state) and a second member, before any screenshot is taken.
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

        // Seed content: a kitchen and a bedroom from templates - both carry daily tasks, so
        // "Idag" has at least three real tasks rather than an empty state.
        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        var roomRows = page.Locator(".floor-room-row");
        await roomRows.Nth(0).GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "+ Lägg till fler rum" }).ClickAsync();
        await roomRows.Nth(1).GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });

        var createFloorButton = page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true });
        await createFloorButton.ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync(new() { Timeout = 10_000 });
        await ShootAsync(page, "03-rum");

        // A second member, so "Hushåll" shows more than a single avatar.
        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();

        // A fresh household starts its creator at zero minutes a day (see PlaneringTests) -
        // without a role, "Idag" would stay empty even with rooms and tasks seeded above.
        await SetAnnasRoleReliablyAsync(page);

        await page.GetByLabel("Namn").FillAsync("Erik");
        await page.GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" }).ClickAsync();

        var addMemberButton = page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" });
        await addMemberButton.ClickAsync();
        await Assertions.Expect(addMemberButton).ToBeEnabledAsync();
        await ShootAsync(page, "04-hushall");

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka", Exact = true }).WaitForAsync();
        await ShootAsync(page, "05-vecka");

        // Visiting "Idag" is what actually generates today's occurrences from the room
        // templates above (see EnsureOccurrencesGenerated in docs/ARCHITECTURE.md §3) and
        // assigns each one to a member. With two brand-new members tied on quota, rotation can
        // land everything on just one of them - rebalancing afterwards is what a real household
        // in that situation would do too, and it is what gives Anna's own "Idag" real content.
        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Anna!" }).WaitForAsync();

        await page.GotoAsync("/rum");
        await page.GetByText("Känns det som att en person gör för mycket?").ClickAsync();

        var rebalanceButton = page.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" });
        await rebalanceButton.ClickAsync();
        await Assertions.Expect(rebalanceButton).ToBeEnabledAsync();

        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Anna!" }).WaitForAsync();
        await ShootAsync(page, "06-idag");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await ShootAsync(page, "07-installningar");

        await CaptureIdagInModeAsync(page, "Bild + text - med bilder för tydlighet", "08-idag-bild-text");
        await CaptureIdagInModeAsync(page, "Stor text - större och tydligare", "09-idag-stor-text");
        await CaptureIdagInModeAsync(page, "En uppgift åt gången - fokusläge", "10-idag-en-i-taget");

        // Mobile: the navigation must become a bottom bar, and every page above must still fit
        // without horizontal scroll or overlap.
        await page.SetViewportSizeAsync(390, 844);

        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Anna!" }).WaitForAsync();
        await ShootAsync(page, "11-idag-mobil");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();
        await ShootAsync(page, "12-rum-mobil");

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka", Exact = true }).WaitForAsync();
        await ShootAsync(page, "13-vecka-mobil");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();
        await ShootAsync(page, "14-hushall-mobil");
    }

    /// <summary>Switches "Min visning" to the given presentation mode, saves, then screenshots
    /// "Idag" in that mode - see docs/PRODUCT.md §7, individual presentation.</summary>
    private static async Task CaptureIdagInModeAsync(IPage page, string presentationLabel, string screenshotName)
    {
        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await page.GetByLabel(presentationLabel).CheckAsync();

        var saveButton = page.GetByRole(AriaRole.Button, new() { Name = "Spara" });
        await saveButton.ClickAsync();
        await Assertions.Expect(saveButton).ToBeEnabledAsync();

        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Anna!" }).WaitForAsync();
        await ShootAsync(page, screenshotName);
    }

    /// <summary>
    /// Picking a role fires two independent PUTs (role, weekly budget) concurrently by design
    /// (see docs/ARCHITECTURE.md §3) - a documented, pre-existing race under load that can
    /// occasionally drop the budget write even though the role itself sticks (the same
    /// flakiness <c>HushallTests.Changing_a_members_role_...</c> is already known to hit).
    /// Retries the pick until "Min vecka" actually shows a non-zero Monday, rather than working
    /// around the race in application code, which is out of scope for this step.
    /// </summary>
    private static async Task SetAnnasRoleReliablyAsync(IPage page)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var annaRole = page.GetByLabel("Roll för Anna");
            await annaRole.SelectOptionAsync(new SelectOptionValue { Label = "Vuxen, jobbar heltid" });
            await Assertions.Expect(annaRole).ToHaveValueAsync("AdultFullTime");

            await page.GotoAsync("/vecka");
            var monday = page.Locator(".list-item").First;
            await monday.WaitForAsync();
            var stuck = !(await monday.InnerTextAsync()).Contains("Ingen tid");

            await page.GotoAsync("/hushall");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();

            if (stuck)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Anna's weekly budget never stuck after 5 attempts - see the race described above.");
    }

    private static async Task ShootAsync(IPage page, string name)
        => await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(OutputDirectory, $"{name}.png"),
            FullPage = true
        });
}
