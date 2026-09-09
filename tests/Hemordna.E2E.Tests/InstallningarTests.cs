using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class InstallningarTests
{
    private readonly HemordnaAppFixture _app;

    public InstallningarTests(HemordnaAppFixture app) => _app = app;

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
}
