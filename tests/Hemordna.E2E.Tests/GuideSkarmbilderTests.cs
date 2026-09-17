using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Captures the mobile screenshots the HTML user guides under
/// <c>src/Hemordna.Client/wwwroot/hjalp/</c> embed. Every shot is taken at 390 × 844, because the
/// guides are read on a phone and show the app as a phone renders it.
/// </summary>
/// <remarks>
/// Writes to <c>HEMORDNA_SCREENSHOT_DIR</c> when set, a temp folder otherwise - the same opt-in
/// <see cref="SkarmbilderTests"/> already uses, so an ordinary test run never rewrites files in
/// the source tree. To refresh the guides' own images after a UI change, point it at them:
/// <code>
/// HEMORDNA_SCREENSHOT_DIR=src/Hemordna.Client/wwwroot/hjalp/img \
///   dotnet test tests/Hemordna.E2E.Tests --filter FullyQualifiedName~GuideSkarmbilderTests
/// </code>
/// The file names are the guides' own contract - <c>sv/familjen.html</c> and
/// <c>sv/hushallsansvarig.html</c> reference them by name, so renaming one here means renaming it
/// there too.
/// </remarks>
[Collection(HemordnaAppCollection.Name)]
public class GuideSkarmbilderTests
{
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;

    private readonly HemordnaAppFixture _app;

    public GuideSkarmbilderTests(HemordnaAppFixture app) => _app = app;

    private static string OutputDirectory
    {
        get
        {
            var directory = Environment.GetEnvironmentVariable("HEMORDNA_SCREENSHOT_DIR")
                ?? Path.Combine(Path.GetTempPath(), "hemordna-guide");

            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    /// <summary>A short settle before every shot: sheets slide in, and a half-drawn sheet in a
    /// guide reads as a bug rather than as the app.</summary>
    private static async Task ShootAsync(IPage page, string name)
    {
        await Task.Delay(350);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(OutputDirectory, $"mobil_{name}.png"),
            FullPage = true
        });
    }

    /// <summary>
    /// Viewport-only, NOT full-page - for a screen with an open, <c>position: fixed</c> sheet
    /// over a page that has more content further down (a room list, a household page with its
    /// own footer links). Playwright's <c>FullPage</c> capture scrolls and stitches the whole
    /// document; a fixed-position sheet stays pinned to the same screen position in every
    /// stitched slice, so slices below the first show real page content bleeding in where the
    /// sheet should continue. A viewport-only shot has no scrolling to stitch, so it has nothing
    /// to bleed - see ShootAsync's own screenshots (e.g. mobil_medlem.png) for the artifact this
    /// avoids.
    /// </summary>
    private static async Task ShootViewportAsync(IPage page, string name)
    {
        await Task.Delay(350);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(OutputDirectory, $"mobil_{name}.png"),
            FullPage = false
        });
    }

    [Fact]
    public async Task Capture_every_screen_the_guides_use()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hemordna" }).WaitForAsync();
        await ShootAsync(page, "logga-in");

        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Anna");
        await page.GetByLabel("E-post").FillAsync($"guide-{Guid.NewGuid():N}@example.com");
        await page.GetByLabel("Lösenord").FillAsync(SignUpHelper.Password);
        await ShootAsync(page, "skapa-konto");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Välkommen!" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Andersson");
        await ShootAsync(page, "skapa-hushall");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync(new() { Timeout = 15_000 });

        // A brand new household starts its creator at zero minutes a day, so "Idag" would stay
        // empty however many rooms are seeded below - see PlaneringTests.
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 45, tuesday = 45, wednesday = 45, thursday = 45, friday = 45, saturday = 90, sunday = 90 });

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();
        await ShootAsync(page, "rum-tomt");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        var roomRows = page.Locator(".floor-room-row");
        await roomRows.Nth(0).GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByText("+ Lägg till fler rum").ClickAsync();
        await roomRows.Nth(1).GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Badrum" });
        await ShootAsync(page, "nytt-rum");

        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync(new() { Timeout = 10_000 });
        await ShootAsync(page, "nytt-rum-klart");
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        await ShootAsync(page, "rum");

        // "Planera veckan" - the rooms above already seeded real, weighted tasks via their
        // templates, so the suggestion has something to show.
        await page.GetByRole(AriaRole.Button, new() { Name = "Planera veckan" }).ClickAsync();
        var weeklyPlanSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Planera veckan" });
        await weeklyPlanSheet.WaitForAsync();
        await Assertions.Expect(page.GetByText("Räknar ut ett förslag...")).Not.ToBeVisibleAsync();
        // Weekday budget (45 min) is much smaller than the weekend's (90) - see the seeded
        // weekly-budget above - so the greedy placement concentrates real besök on
        // Saturday/Sunday. Scroll there so the screenshot shows an actual besök, not several
        // "Inga besök" rows in a row.
        await weeklyPlanSheet.GetByText("Söndag").ScrollIntoViewIfNeededAsync();
        await ShootViewportAsync(page, "planera-veckan");
        await weeklyPlanSheet.GetByRole(AriaRole.Button, new() { Name = "Avbryt" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var kitchen = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await kitchen.WaitForAsync();
        await ShootAsync(page, "rumsark");

        await kitchen.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Lägg till uppgift i Kök" });
        await addSheet.GetByLabel("Namn").FillAsync("Vattna blommorna");
        await ShootAsync(page, "lagg-till-uppgift");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await Assertions.Expect(kitchen.GetByRole(AriaRole.Button, new() { Name = "Vattna blommorna" }))
            .ToBeVisibleAsync();

        // Tyngd och Alltid på - båda rader i samma uppgifts TaskOptionsSheet.
        await kitchen.GetByRole(AriaRole.Button, new() { Name = "Vattna blommorna" }).ClickAsync();
        var flowerTaskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Vattna blommorna" });
        await flowerTaskSheet.WaitForAsync();

        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Tyngd" }).ClickAsync();
        await ShootViewportAsync(page, "tyngd");
        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Avbryt" }).ClickAsync();

        // "Alltid på" visas bara för en uppgift med en veckodag att välja - sätt Upprepning till
        // "Varje vecka" först.
        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Upprepning" }).ClickAsync();
        await flowerTaskSheet.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await Assertions.Expect(flowerTaskSheet.Locator("select")).Not.ToBeVisibleAsync();

        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Alltid på" }).ClickAsync();
        await flowerTaskSheet.GetByLabel("Alltid på").SelectOptionAsync("Wednesday");
        await ShootViewportAsync(page, "alltid-pa");
        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await flowerTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await kitchen.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // Visiting "Idag" is what generates today's occurrences from the seeded rooms; the reload
        // is what shows them, since generation happens on the first load.
        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync(new() { Timeout = 15_000 });
        await Task.Delay(1200);
        await page.ReloadAsync();
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync(new() { Timeout = 15_000 });
        await ShootAsync(page, "idag");

        var extra = page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).First;
        if (await extra.CountAsync() > 0)
        {
            await extra.ClickAsync();
            await Task.Delay(600);
            await ShootAsync(page, "extra-uppgift");
            await page.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).First.ClickAsync();
        }

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka", Exact = true }).WaitForAsync();
        await ShootAsync(page, "vecka");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();
        await ShootAsync(page, "hushall");

        await page.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var inviteSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        await inviteSheet.WaitForAsync();
        await ShootAsync(page, "bjud-in");
        var inviteCode = (await inviteSheet.Locator(".invite-code").InnerTextAsync()).Trim();
        await inviteSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        var annaSheet = await HushallHelper.OpenMemberSheetAsync(page, "Anna");
        await ShootAsync(page, "medlem");

        await annaSheet.GetByText("Hur mycket orkar personen per veckodag").ClickAsync();
        // The disclosure's own weekday rows sit below the fold at this Half-detent sheet's
        // height - scroll to the first one so the screenshot actually shows the level pickers,
        // not just the intro paragraph above them.
        await annaSheet.GetByRole(AriaRole.Group, new() { Name = "Ork Måndag", Exact = true }).ScrollIntoViewIfNeededAsync();
        await ShootViewportAsync(page, "ork");

        await annaSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Pausa hushållet" }).ClickAsync();
        var pauseSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Pausa hushållet" });
        await pauseSheet.WaitForAsync();
        await ShootAsync(page, "pausa-hushallet");
        await pauseSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" }).ClickAsync();
        var rebalanceSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Balansera om vem som gör vad" });
        await rebalanceSheet.WaitForAsync();
        await ShootAsync(page, "balansera-om");
        await rebalanceSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Rensa hushållets data" }).ClickAsync();
        var resetSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Rensa hushållets data" });
        await resetSheet.WaitForAsync();
        await ShootAsync(page, "rensa-data");
        await resetSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await ShootAsync(page, "installningar");

        // Erik joins with the code, so he does NOT manage the household - the pair of shots the
        // "vem får ändra vad" chapter compares side by side.
        var erikPage = await _app.NewPageAsync();
        await erikPage.SetViewportSizeAsync(PhoneWidth, PhoneHeight);
        await SignUpHelper.RegisterAsync(erikPage, "Erik");
        await erikPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await erikPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await ShootAsync(erikPage, "ga-med-med-kod");
        await erikPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await erikPage.Locator("h1", new() { HasText = "Erik" }).WaitForAsync(new() { Timeout = 15_000 });

        await erikPage.GotoAsync("/rum");
        await erikPage.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();
        await ShootAsync(erikPage, "rum-utan-kryss");

        await erikPage.GotoAsync("/hushall");
        await erikPage.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();
        await ShootAsync(erikPage, "hushall-utan-kryss");
    }
}
