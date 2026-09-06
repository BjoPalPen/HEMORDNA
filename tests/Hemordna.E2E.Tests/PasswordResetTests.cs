using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class PasswordResetTests
{
    private readonly HemordnaAppFixture _app;

    public PasswordResetTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Requesting_a_reset_link_shows_the_same_confirmation_for_an_unknown_address()
    {
        var page = await _app.NewPageAsync();
        await page.GotoAsync("/logga-in");

        await page.GetByRole(AriaRole.Button, new() { Name = "Glömt lösenord?" }).ClickAsync();
        await page.GetByLabel("E-post").FillAsync("does-not-exist@example.com");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skicka återställningslänk" }).ClickAsync();

        // No number, no "user not found" - see AuthEndpoints.ForgotPasswordAsync for why.
        await page.GetByText("Om adressen finns hos oss").WaitForAsync();
    }

    [Fact]
    public async Task Following_the_reset_link_lets_the_user_set_a_new_password_and_sign_in_with_it()
    {
        var page = await _app.NewPageAsync();
        var email = $"e2e-reset-{Guid.NewGuid():N}@example.com";

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Reset Persson");
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByLabel("Lösenord").FillAsync("Ursprungligt-Losenord-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Persson", new() { Timeout = 15_000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Reset Persson!" })
            .WaitForAsync(new() { Timeout = 15_000 });

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Button, new() { Name = "Glömt lösenord?" }).ClickAsync();
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skicka återställningslänk" }).ClickAsync();
        await page.GetByText("Om adressen finns hos oss").WaitForAsync();

        // No real Resend account locally - the API logged the e-mail to DevEmailOutbox
        // instead of sending it (see LoggingEmailSender). Fetch it back through the dev-only
        // endpoint the same way a test inbox would, and pull the reset link out of it.
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        var emailBody = await (await http.GetAsync(
            $"/api/auth/dev/last-email?email={Uri.EscapeDataString(email)}")).Content.ReadAsStringAsync();
        var link = Regex.Match(emailBody, @"href=""([^""]+)""").Groups[1].Value;
        var resetPath = new Uri(link).PathAndQuery;

        await page.GotoAsync(resetPath);
        await page.GetByLabel("Nytt lösenord", new() { Exact = true }).FillAsync("Alldeles-Nytt-Losenord-2026!");
        await page.GetByLabel("Bekräfta nytt lösenord").FillAsync("Alldeles-Nytt-Losenord-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Byt lösenord" }).ClickAsync();
        await page.GetByText("Lösenordet är bytt.").WaitForAsync();

        await page.GetByRole(AriaRole.Link, new() { Name = "Logga in" }).ClickAsync();
        await page.GetByLabel("E-post").FillAsync(email);
        await page.GetByLabel("Lösenord").FillAsync("Alldeles-Nytt-Losenord-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga in" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Reset Persson!" })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task An_expired_or_unknown_token_shows_an_error_instead_of_changing_anything()
    {
        var page = await _app.NewPageAsync();
        await page.GotoAsync("/aterstall-losenord?email=nobody%40example.com&token=not-a-real-token");

        await page.GetByLabel("Nytt lösenord", new() { Exact = true }).FillAsync("Alldeles-Nytt-Losenord-2026!");
        await page.GetByLabel("Bekräfta nytt lösenord").FillAsync("Alldeles-Nytt-Losenord-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Byt lösenord" }).ClickAsync();

        await page.GetByText("Länken är ogiltig eller har gått ut.").WaitForAsync();
    }
}
