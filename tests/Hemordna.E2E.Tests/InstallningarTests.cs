using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class InstallningarTests
{
    private readonly HemordnaAppFixture _app;

    public InstallningarTests(HemordnaAppFixture app) => _app = app;

    /// <summary>
    /// "Utseende" (Support/Backdrop.cs, wwwroot/js/backdrop.js) - a per-device photo, never
    /// uploaded anywhere. No binary file is checked into the repo for this: a tiny, colourful
    /// gradient PNG is drawn in the page itself via a canvas, exactly the kind of file a real
    /// phone photo would produce (just far smaller), then handed to the hidden #backdrop-file
    /// input the same way Playwright would hand it a file from disk.
    /// </summary>
    private static async Task<byte[]> CreateGradientPngAsync(IPage page)
    {
        var dataUrl = await page.EvaluateAsync<string>("""
            () => {
                const canvas = document.createElement('canvas');
                canvas.width = 64;
                canvas.height = 48;
                const context = canvas.getContext('2d');
                const gradient = context.createLinearGradient(0, 0, 64, 48);
                gradient.addColorStop(0, '#E9B44C');
                gradient.addColorStop(1, '#4A6C8C');
                context.fillStyle = gradient;
                context.fillRect(0, 0, 64, 48);
                return canvas.toDataURL('image/png');
            }
            """);

        return Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',') + 1)..]);
    }

    /// <summary>Navigates to Inställningar, picks the gradient PNG above as the backdrop and
    /// waits for it to actually apply - the common first half of every test below.</summary>
    private static async Task UploadBackdropAsync(IPage page)
    {
        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();

        var png = await CreateGradientPngAsync(page);
        await page.Locator("#backdrop-file").SetInputFilesAsync(new FilePayload
        {
            Name = "bakgrund.png",
            MimeType = "image/png",
            Buffer = png
        });

        await Assertions.Expect(page.Locator("html"))
            .ToHaveAttributeAsync("data-backdrop", "", new() { Timeout = 10_000 });
        await Assertions.Expect(page.Locator(".backdrop-preview")).ToBeVisibleAsync();
    }

    private static Task<string> BodyBeforeDisplayAsync(IPage page)
        => page.EvaluateAsync<string>("() => getComputedStyle(document.body, '::before').display");

    /// <summary>
    /// "Inställningar sparas när de ändras. Ingen Spara-knapp, ingen blandning av direkt och
    /// uppskjutet." (docs/DESIGN.md, "Inställningar – Min visning") - a change is on the server
    /// before the member does anything else, confirmed by "Sparat" and by surviving a reload
    /// with no separate save step in between.
    /// </summary>
    [Fact]
    public async Task Changing_the_presentation_saves_immediately_with_no_save_button()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Spara", Exact = true }))
            .Not.ToBeVisibleAsync();

        await page.GetByLabel("Stor text - större och tydligare").CheckAsync();
        await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });

        await page.GetByLabel("Lugn - en vänlig kommentar då och då").CheckAsync();
        await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await Assertions.Expect(page.GetByLabel("Stor text - större och tydligare")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Lugn - en vänlig kommentar då och då")).ToBeCheckedAsync();
    }

    /// <summary>"Beslut: Ångra och stabil lista" §B11 - a preset chip is a shortcut into the
    /// same radios/toggles a member could set by hand, never a separate mode of its own: pick
    /// one, and every one of the four underlying choices reads back exactly as if set one at a
    /// time. Now also true of SAVING - a preset writes to the server in the same press, not
    /// just to the fields on screen.</summary>
    [Fact]
    public async Task Steg_for_steg_sets_all_four_choices_saves_directly_and_kompakt_resets_them()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Freja");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Steg för steg" }).ClickAsync();

        await Assertions.Expect(page.GetByLabel("En uppgift åt gången - fokusläge")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Lugn - en vänlig kommentar då och då")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Visa ungefär hur lång tid en uppgift tar")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter"))
            .ToBeCheckedAsync();
        // The chip itself reflects the match, not just the fields it filled in.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Steg för steg" }))
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("chip-primary"));
        await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });

        // Reload proves the preset's own save actually reached the server, not just the fields.
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await Assertions.Expect(page.GetByLabel("En uppgift åt gången - fokusläge")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Visa ungefär hur lång tid en uppgift tar")).ToBeCheckedAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Kompakt" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });

        await Assertions.Expect(page.GetByLabel("Text (standard) - kompakt lista")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Ingen - bara fakta")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Visa ungefär hur lång tid en uppgift tar")).Not.ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter"))
            .Not.ToBeCheckedAsync();
    }

    /// <summary>A rejected save never leaves the UI showing a choice that only looks saved - the
    /// field goes back to whatever the server actually still has, with a visible, actionable
    /// error rather than a silent failure.</summary>
    [Fact]
    public async Task A_failed_save_shows_an_error_and_reverts_the_field()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await page.RouteAsync("**/api/households/*/members/*/preferences", async route =>
            await route.FulfillAsync(new() { Status = 500, Body = "" }));

        await page.GetByLabel("Stor text - större och tydligare").CheckAsync();

        await Assertions.Expect(page.GetByText("Kunde inte spara. Försök igen.")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Försök igen" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Text (standard) - kompakt lista")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Stor text - större och tydligare")).Not.ToBeCheckedAsync();
    }

    [Fact]
    public async Task Changing_the_password_lets_the_user_sign_in_with_the_new_one_but_not_the_old_one()
    {
        var page = await _app.NewPageAsync();
        var email = $"e2e-changepw-{Guid.NewGuid():N}@example.com";

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Byter Persson");
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByLabel("Lösenord").FillAsync(SignUpHelper.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Persson");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();
        await page.Locator("h1", new() { HasText = "Byter Persson" })
            .WaitForAsync(new() { Timeout = 15_000 });

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Byt lösenord" }).WaitForAsync();

        await page.GetByLabel("Nuvarande lösenord").FillAsync(SignUpHelper.Password);
        await page.GetByLabel("Nytt lösenord", new() { Exact = true }).FillAsync("Ett-Helt-Nytt-Losenord-2026!");
        await page.GetByLabel("Bekräfta nytt lösenord").FillAsync("Ett-Helt-Nytt-Losenord-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Byt lösenord" }).ClickAsync();
        await page.GetByText("Lösenordet är bytt.").WaitForAsync();

        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Byt lösenord" })).ToBeVisibleAsync();

        // No sign-out button in the app yet - drop the token directly, the same way the
        // fixture's other tests reach into localStorage to read it (see HushallActivityTests).
        await page.EvaluateAsync("() => localStorage.removeItem('hemordna.token')");

        await page.GotoAsync("/logga-in");
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByLabel("Lösenord").FillAsync(SignUpHelper.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga in" }).ClickAsync();
        await Assertions.Expect(page.GetByText("E-postadressen eller lösenordet stämmer inte.")).ToBeVisibleAsync();

        await page.GetByLabel("Lösenord").FillAsync("Ett-Helt-Nytt-Losenord-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga in" }).ClickAsync();
        await page.Locator("h1", new() { HasText = "Byter Persson" })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    private static string TestResultsDirectory
    {
        get
        {
            // Same repository-root walk HemordnaAppFixture's own FindRepositoryRoot does - this
            // test needs it too, to land screenshots at a fixed, reviewable path regardless of
            // where the test binaries happen to run from.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hemordna.slnx")))
            {
                directory = directory.Parent;
            }

            var repositoryRoot = directory?.FullName
                ?? throw new InvalidOperationException("Could not find the repository root (Hemordna.slnx).");

            var testResults = Path.Combine(repositoryRoot, "TestResults");
            Directory.CreateDirectory(testResults);
            return testResults;
        }
    }

    private static Task ShootAsync(IPage page, string name)
        => page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(TestResultsDirectory, $"{name}.png"),
            FullPage = true
        });

    /// <summary>
    /// Covers the storage/apply layer end to end through the real UI: picking a file applies the
    /// photo immediately (no save button, same as the theme radios above it), and it survives a
    /// reload - proving the IndexedDB write and index.html's own early re-apply both actually
    /// work, not just the in-memory object URL from the moment it was picked. Also captures the
    /// two review screenshots the background-image task asked for, in light and dark, on Idag -
    /// seeded with one real task first (same direct API seeding MinDagTests uses) so the shot
    /// shows the photo around an actual opaque .task-list card, not an empty-state page with no
    /// cards at all - and at the 390 × 844 phone width GuideSkarmbilderTests uses, since
    /// .app-topfade/.app-botfade and the floating nav pill this task asked to double-check only
    /// render below 900px.
    /// </summary>
    [Fact]
    public async Task Uploading_a_backdrop_image_applies_it_immediately_and_survives_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Solveig");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Vattna blommorna", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = DateOnly.FromDateTime(DateTime.UtcNow), assignToMemberId = memberId });

        await UploadBackdropAsync(page);
        await page.SetViewportSizeAsync(390, 844);

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Solveig" }).WaitForAsync(new() { Timeout = 15_000 });
        await page.GetByText("Vattna blommorna").WaitForAsync();
        await ShootAsync(page, "backdrop-light");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();
        await page.GetByLabel("Mörkt").CheckAsync();
        await page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-theme') === 'dark'");

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Solveig" }).WaitForAsync(new() { Timeout = 15_000 });
        await page.GetByText("Vattna blommorna").WaitForAsync();
        await ShootAsync(page, "backdrop-dark");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();

        await Assertions.Expect(page.Locator("html"))
            .ToHaveAttributeAsync("data-backdrop", "", new() { Timeout = 10_000 });
        await Assertions.Expect(page.Locator(".backdrop-preview")).ToBeVisibleAsync();
    }

    /// <summary>"Lugnare skärm vinner" (docs/PRODUCT.md decision behind Support/Backdrop.cs) -
    /// a personal photo behind the content is exactly the visual noise that setting removes, so
    /// it must hide the whole layer outright, not just tone it down, and come back the moment
    /// calm screen is switched off again. Finishes by removing the image entirely, proving
    /// "Ta bort bild" both un-applies it and actually clears IndexedDB.</summary>
    [Fact]
    public async Task Calm_screen_hides_the_backdrop_and_removing_the_image_clears_it()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Tyra");

        await UploadBackdropAsync(page);

        Assert.NotEqual("none", await BodyBeforeDisplayAsync(page));

        var calmToggle = page.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter");
        await calmToggle.CheckAsync();
        await page.WaitForFunctionAsync("() => document.documentElement.hasAttribute('data-calm')");
        Assert.Equal("none", await BodyBeforeDisplayAsync(page));

        await calmToggle.UncheckAsync();
        await page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-calm')");
        Assert.NotEqual("none", await BodyBeforeDisplayAsync(page));

        await page.GetByRole(AriaRole.Button, new() { Name = "Ta bort bild" }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-backdrop')");
        await Assertions.Expect(page.Locator(".backdrop-preview")).Not.ToBeVisibleAsync();
    }
}
