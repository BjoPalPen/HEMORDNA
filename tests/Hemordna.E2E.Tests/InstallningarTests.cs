using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class InstallningarTests
{
    private readonly HemordnaAppFixture _app;

    public InstallningarTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Changing_the_presentation_and_motivation_persists_across_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await page.GetByLabel("Stor text - större och tydligare").CheckAsync();
        await page.GetByLabel("Lugn - en vänlig kommentar då och då").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        // Wait for the save to actually finish (the button reads "Sparar..." while in flight)
        // before reloading, or the reload can race the still-in-flight PUT.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Spara" })).ToBeEnabledAsync();

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await Assertions.Expect(page.GetByLabel("Stor text - större och tydligare")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Lugn - en vänlig kommentar då och då")).ToBeCheckedAsync();
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
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Byter Persson!" })
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
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Byter Persson!" })
            .WaitForAsync(new() { Timeout = 15_000 });
    }
}
