using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Walks the paths a real person takes: sign up, name the household, see the day, tick
/// something off. These verify the screens actually render and talk to the API - something
/// unit tests over fakes cannot show.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class MinDagTests
{
    private readonly HemordnaAppFixture _app;

    public MinDagTests(HemordnaAppFixture app) => _app = app;

    private static string UniqueEmail() => $"e2e-{Guid.NewGuid():N}@example.com";

    private const string Password = "Hemordna-E2E-2026!";

    /// <summary>Registers, names a household and lands on Min dag.</summary>
    private static async Task SignUpAsync(IPage page, string displayName)
    {
        await page.GotoAsync("/logga-in");

        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync(displayName);
        await page.GetByLabel("E-post").FillAsync(UniqueEmail());
        await page.GetByLabel("Lösenord").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();

        await page.GetByLabel("Hushållets namn").FillAsync("Familjen Andersson");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = $"Hej {displayName}!" })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task The_sign_in_page_shows_the_brand()
    {
        var page = await _app.NewPageAsync();

        await page.GotoAsync("/logga-in");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hemordna" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Ett enklare hem, en lugnare vardag"))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Signing_in_with_the_wrong_password_says_so_without_revealing_which_part()
    {
        var page = await _app.NewPageAsync();

        await page.GotoAsync("/logga-in");
        await page.GetByLabel("E-post").FillAsync(UniqueEmail());
        await page.GetByLabel("Lösenord").FillAsync("fel-losenord-som-inte-finns");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga in" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("E-postadressen eller lösenordet stämmer inte.");
    }

    [Fact]
    public async Task A_new_user_is_asked_to_name_their_household()
    {
        var page = await _app.NewPageAsync();

        await page.GotoAsync("/logga-in");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync("Anna");
        await page.GetByLabel("E-post").FillAsync(UniqueEmail());
        await page.GetByLabel("Lösenord").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Välkommen!" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Min_dag_greets_the_person_by_name()
    {
        var page = await _app.NewPageAsync();

        await SignUpAsync(page, "Anna");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hej Anna!" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_empty_day_says_so_calmly_and_never_scolds()
    {
        var page = await _app.NewPageAsync();

        await SignUpAsync(page, "Bjorn");

        await Assertions.Expect(page.GetByText("Inget är inplanerat idag.")).ToBeVisibleAsync();

        // The tone rules in docs/PRODUCT.md are a product requirement, not a preference.
        var body = await page.Locator("body").InnerTextAsync();
        foreach (var forbidden in new[] { "ligger efter", "missade", "streak", "i rad" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_signed_out_visitor_is_sent_to_sign_in()
    {
        var page = await _app.NewPageAsync();

        await page.GotoAsync("/");

        await page.WaitForURLAsync("**/logga-in", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Changing_the_normal_week_persists_across_a_reload()
    {
        var page = await _app.NewPageAsync();

        await SignUpAsync(page, "Cecilia");

        await page.GetByRole(AriaRole.Button, new() { Name = "Ändra din vanliga vecka" }).ClickAsync();
        await page.GetByLabel("Mån").FillAsync("45");
        await page.GetByRole(AriaRole.Button, new() { Name = "Spara vanlig vecka" }).ClickAsync();

        // The editor closes on save, going back to the button that opens it.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ändra din vanliga vecka" }))
            .ToBeVisibleAsync();

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ändra din vanliga vecka" }).ClickAsync();

        await Assertions.Expect(page.GetByLabel("Mån")).ToHaveValueAsync("45");
    }

    /// <summary>
    /// Hemordna is a Swedish app, so the date is Swedish even to someone whose browser is not.
    /// Blazor loads its globalization data based on the browser language, so an English browser
    /// leaves the app without Swedish unless the app pins its own culture.
    /// </summary>
    [Fact]
    public async Task The_date_is_Swedish_even_when_the_browser_is_English()
    {
        var page = await _app.NewPageAsync(locale: "en-US");

        await SignUpAsync(page, "Sara");

        // TextContent, not InnerText: the stylesheet capitalises the label, and what is under
        // test is how the app formats the date, not how the design presents it.
        var label = await page.Locator(".day-date").TextContentAsync();

        string[] swedishWeekdays =
            ["måndag", "tisdag", "onsdag", "torsdag", "fredag", "lördag", "söndag"];

        Assert.True(
            label is not null
                && swedishWeekdays.Any(day => label.StartsWith(day, StringComparison.Ordinal)),
            $"The date read '{label}', which is not a Swedish weekday.");
    }
}
