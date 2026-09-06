using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class PasskeyTests
{
    private readonly HemordnaAppFixture _app;

    public PasskeyTests(HemordnaAppFixture app) => _app = app;

    /// <summary>
    /// Chromium ships a virtual, software authenticator behind the CDP WebAuthn domain
    /// specifically so tests do not need real Face ID/Touch ID/Windows Hello hardware -
    /// enabling it here is what lets navigator.credentials.create()/get() resolve at all in a
    /// headless browser instead of hanging on a real device prompt forever.
    /// </summary>
    private static async Task AddVirtualAuthenticatorAsync(IPage page)
    {
        var cdp = await page.Context.NewCDPSessionAsync(page);
        await cdp.SendAsync("WebAuthn.enable", new Dictionary<string, object> { ["enableUI"] = false });
        await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new Dictionary<string, object>
            {
                ["protocol"] = "ctap2",
                ["transport"] = "internal",
                ["hasResidentKey"] = true,
                ["hasUserVerification"] = true,
                ["isUserVerified"] = true,
                ["automaticPresenceSimulation"] = true
            }
        });
    }

    [Fact]
    public async Task Adding_a_passkey_lets_the_user_sign_in_with_it_instead_of_the_password()
    {
        var page = await _app.NewPageAsync();
        await AddVirtualAuthenticatorAsync(page);

        var email = $"e2e-passkey-{Guid.NewGuid():N}@example.com";

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Nyckel Persson");
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByLabel("Lösenord").FillAsync(SignUpHelper.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Nyckel", new() { Timeout = 15_000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Nyckel Persson!" })
            .WaitForAsync(new() { Timeout = 15_000 });

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Biometrisk inloggning" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till den här enheten" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ta bort" }).WaitForAsync(new() { Timeout = 10_000 });

        await page.GotoAsync("/mer");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga ut" }).ClickAsync();
        await page.GetByRole(AriaRole.Tab, new() { Name = "Logga in" }).WaitForAsync();

        // No e-mail entered anywhere - the whole point is that the browser offers the
        // discoverable passkey itself, without asking who is signing in first.
        await page.GetByRole(AriaRole.Button, new() { Name = "Fortsätt med Face ID / fingeravtryck" })
            .ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Nyckel Persson!" })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Removing_a_passkey_takes_it_off_the_list_and_it_can_no_longer_sign_in()
    {
        var page = await _app.NewPageAsync();
        await AddVirtualAuthenticatorAsync(page);

        var email = $"e2e-passkey-{Guid.NewGuid():N}@example.com";

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Borttagen Persson");
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByLabel("Lösenord").FillAsync(SignUpHelper.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Borttagen", new() { Timeout = 15_000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Borttagen Persson!" })
            .WaitForAsync(new() { Timeout = 15_000 });

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till den här enheten" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ta bort" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ta bort" })).Not.ToBeVisibleAsync();

        await page.GotoAsync("/mer");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga ut" }).ClickAsync();
        await page.GetByRole(AriaRole.Tab, new() { Name = "Logga in" }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Fortsätt med Face ID / fingeravtryck" })
            .ClickAsync();

        await page.GetByText("Det gick inte att logga in med Face ID/fingeravtryck. Prova lösenordet i stället.")
            .WaitForAsync(new() { Timeout = 15_000 });
    }
}
