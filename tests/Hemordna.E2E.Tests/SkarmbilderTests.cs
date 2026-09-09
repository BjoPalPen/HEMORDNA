using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        await page.Locator("h1", new() { HasText = "Anna" })
            .WaitForAsync(new() { Timeout = 15_000 });

        // Seed content: a kitchen and a bedroom from templates - both carry daily tasks, so
        // "Idag" has at least three real tasks rather than an empty state.
        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });

        var roomRows = page.Locator(".floor-room-row");
        await roomRows.Nth(0).GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByText("+ Lägg till fler rum").ClickAsync();
        await roomRows.Nth(1).GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });

        var createFloorButton = page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true });
        await createFloorButton.ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync(new() { Timeout = 10_000 });
        await ShootAsync(page, "03-nytt-rum-sheet");
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        await ShootAsync(page, "03-rum");

        // A room's own sheet (RoomSheet.razor), the primary new surface step 3 added.
        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var kitchenSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await kitchenSheet.WaitForAsync();
        await ShootAsync(page, "03z-room-sheet");
        await kitchenSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // A second member, so "Hushåll" shows more than a single avatar.
        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();

        // A fresh household starts its creator at zero minutes a day (see PlaneringTests) -
        // without a role, "Idag" would stay empty even with rooms and tasks seeded above.
        await SetAnnasRoleReliablyAsync(page);

        // MemberSheet.razor - a member's own role/pause/removal, the primary new surface step 4
        // adds. Reopened fresh (rather than reused from the retry loop above) purely for the
        // screenshot's own timing.
        var annaSheet = await HushallHelper.OpenMemberSheetAsync(page, "Anna");
        await ShootAsync(page, "04z-member-sheet");
        await annaSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // "Bjud in" - both the invite code and the nested "utan eget konto" form live here now.
        await page.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var inviteSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        await ShootAsync(page, "04y-bjud-in-sheet");

        await inviteSheet.GetByText("Eller lägg till en medlem utan eget konto").ClickAsync();
        await inviteSheet.GetByLabel("Namn").FillAsync("Erik");
        await inviteSheet.Locator("form").GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" }).ClickAsync();

        var addMemberButton = inviteSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" });
        await addMemberButton.ClickAsync();
        await Assertions.Expect(addMemberButton).ToBeEnabledAsync();
        await inviteSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        await ShootAsync(page, "04-hushall");

        // "Pausa hushållet" - shares its own text with the listrow that opens it, so scoped to
        // the Dialog once open, same as the rebalance sheet below.
        await page.GetByRole(AriaRole.Button, new() { Name = "Pausa hushållet" }).ClickAsync();
        var pauseSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Pausa hushållet" });
        await ShootAsync(page, "04x-pausa-sheet");
        await pauseSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka", Exact = true }).WaitForAsync();
        await ShootAsync(page, "05-vecka");

        // Visiting "Idag" is what actually generates today's occurrences from the room
        // templates above (see EnsureOccurrencesGenerated in docs/ARCHITECTURE.md §3) and
        // assigns each one to a member. With two brand-new members tied on quota, rotation can
        // land everything on just one of them - rebalancing afterwards is what a real household
        // in that situation would do too, and it is what gives Anna's own "Idag" real content.
        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync();

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" }).ClickAsync();
        var rebalanceSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Balansera om vem som gör vad" });
        var rebalanceButton = rebalanceSheet.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" });
        await rebalanceButton.ClickAsync();
        await Assertions.Expect(rebalanceButton).ToBeEnabledAsync();

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync();
        await ShootAsync(page, "06-idag");

        // BottomSheet.razor in its actual chip-triggered use, not just built-and-unused.
        await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Extra uppgift" }).WaitForAsync();
        await ShootAsync(page, "06z-extra-uppgift-sheet");
        await page.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await ShootAsync(page, "07-installningar");

        await page.GetByRole(AriaRole.Button, new() { Name = "Steg för steg" }).ClickAsync();
        await Assertions.Expect(page.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter"))
            .ToBeCheckedAsync();
        await ShootAsync(page, "07b-installningar-steg-for-steg");

        // "Lugnare skärm" applies immediately and persists (localStorage, not "Spara") - undo it
        // before the mode captures below, or every later screenshot in this run would carry it.
        await page.GetByRole(AriaRole.Button, new() { Name = "Kompakt" }).ClickAsync();
        await Assertions.Expect(page.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter"))
            .Not.ToBeCheckedAsync();

        await CaptureIdagInModeAsync(page, "Bild + text - med bilder för tydlighet", "08-idag-bild-text");
        await CaptureIdagInModeAsync(page, "Stor text - större och tydligare", "09-idag-stor-text");
        await CaptureIdagInModeAsync(page, "En uppgift åt gången - fokusläge", "10-idag-en-i-taget");

        // Back to the default mode - otherwise "11-idag-mobil" below would still show whatever
        // mode the loop above left it in, not the ordinary grouped list.
        await SetPresentationModeAsync(page, "Text (standard) - kompakt lista");

        // Mobile: the navigation must become a bottom bar, and every page above must still fit
        // without horizontal scroll or overlap.
        await page.SetViewportSizeAsync(390, 844);

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync();
        await ShootAsync(page, "11-idag-mobil");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();
        await ShootAsync(page, "12-rum-mobil");

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka", Exact = true }).WaitForAsync();
        await ShootAsync(page, "13-vecka-mobil");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();
        await ShootAsync(page, "14-hushall-mobil");
    }

    /// <summary>Steg 5: dark tokens (docs/ARCHITECTURE.md "Ny form"), applied automatically from
    /// an emulated OS dark preference - the same mechanism most people will actually hit, rather
    /// than the explicit Inställningar override ThemeTests.cs already covers on its own.</summary>
    [Fact]
    public async Task Capture_dark_mode_screens()
    {
        var page = await _app.NewPageAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Nils");
        await page.GetByLabel("E-post").FillAsync($"dark-{Guid.NewGuid():N}@example.com");
        await page.GetByLabel("Lösenord").FillAsync("Hemordna-E2E-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Välkommen!" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Nilsson");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();
        await page.Locator("h1", new() { HasText = "Nils" }).WaitForAsync(new() { Timeout = 15_000 });
        await ShootAsync(page, "d01-idag");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.Locator(".floor-room-row").First.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync(new() { Timeout = 10_000 });
        await ShootAsync(page, "d02-nytt-rum-sheet");
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        await ShootAsync(page, "d03-rum");

        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" }).WaitForAsync();
        await ShootAsync(page, "d04-room-sheet");
        await page.Keyboard.PressAsync("Escape");

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka", Exact = true }).WaitForAsync();
        await ShootAsync(page, "d05-vecka");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Nilsson" }).WaitForAsync();
        await ShootAsync(page, "d06-hushall");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nils" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Nils" }).WaitForAsync();
        await ShootAsync(page, "d07-member-sheet");
        await page.Keyboard.PressAsync("Escape");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();
        await ShootAsync(page, "d08-installningar");

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Nils" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Extra uppgift" }).WaitForAsync();
        await ShootAsync(page, "d09-extra-uppgift-sheet");
    }

    /// <summary>"Sju enkla lösningar" - its own light-weight household (HTTP seeding, not the
    /// full UI-driven room-template flow above) rather than threading four very differently
    /// shaped scenarios (orken, ett pågående, fokusläge, utskrift) into the already long main
    /// walkthrough - the same reasoning Capture_dark_mode_screens above already follows.</summary>
    [Fact]
    public async Task Capture_the_seven_enkla_losningar_screens()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Mika");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var area = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Kök" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var areaId = area.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Diska", "Dammsuga", "Torka golvet"];

        foreach (var name in names)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks",
                new { name, estimatedMinutes = 15, areaId, description = "Ta fram hinken\nTorka torrt" }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        // "Hur är orken idag?" - the three-way picker, not yet answered.
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" }).WaitForAsync();
        await ShootAsync(page, "06f-idag-orken");

        // "Jag börjar nu" - the started row's own "Pågår" chip, moved first in its room.
        await page.Locator(".task-expand").First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Jag börjar nu" }).ClickAsync();
        await ShootAsync(page, "06g-idag-pagar");

        // Fokusläget: "Läs upp" alongside the rest of .focus-actions.
        await page.GotoAsync("/installningar");
        await page.GetByLabel("En uppgift åt gången - fokusläge").CheckAsync();
        await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        await page.GotoAsync("/");
        await page.Locator(".focus-card").WaitForAsync();
        await ShootAsync(page, "06h-fokus-lasupp");

        // Utskrift - the print-only view, A4-width per the uppdrag's own verification step.
        await page.SetViewportSizeAsync(794, 1123);
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        await page.Locator(".print-only").WaitForAsync();
        await ShootAsync(page, "06i-utskrift");
    }

    /// <summary>Switches "Min visning" to the given presentation mode and saves - see
    /// docs/PRODUCT.md §7, individual presentation.</summary>
    private static async Task SetPresentationModeAsync(IPage page, string presentationLabel)
    {
        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await page.GetByLabel(presentationLabel).CheckAsync();
        await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });
    }

    private static async Task CaptureIdagInModeAsync(IPage page, string presentationLabel, string screenshotName)
    {
        await SetPresentationModeAsync(page, presentationLabel);

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Anna" }).WaitForAsync();
        await ShootAsync(page, screenshotName);
    }

    /// <summary>
    /// Picking a role fires two independent PUTs (role, weekly budget) concurrently by design
    /// (see docs/ARCHITECTURE.md §3) - a documented, pre-existing race under load that can
    /// occasionally drop the budget write even though the role itself sticks (the same
    /// flakiness <c>HushallTests.Changing_a_members_role_...</c> is already known to hit).
    /// Retries the pick until "Min vecka" actually shows a non-zero Monday, rather than working
    /// around the race in application code, which is out of scope for this step. Role
    /// management moved into MemberSheet in step 4, behind Anna's own avatar.
    /// </summary>
    private static async Task SetAnnasRoleReliablyAsync(IPage page)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Anna");
            var roleButton = sheet.GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" });
            await roleButton.ClickAsync();
            await Assertions.Expect(roleButton).ToHaveClassAsync(new Regex("btn-primary"));
            await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

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
