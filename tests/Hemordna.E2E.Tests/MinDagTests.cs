using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    private static string UniqueEmail() => SignUpHelper.UniqueEmail();

    private const string Password = SignUpHelper.Password;

    private static Task SignUpAsync(IPage page, string displayName) => SignUpHelper.SignUpAsync(page, displayName);

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
    public async Task Choosing_a_role_sets_a_whole_week_without_a_single_daily_choice()
    {
        var page = await _app.NewPageAsync();
        await SignUpAsync(page, "Kristina");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Din vanliga vecka" }).WaitForAsync();
        await page.Locator(".card", new() { HasText = "Din vanliga vecka" })
            .GetByRole(AriaRole.Button, new() { Name = "Pensionär / hemma dagtid" }).ClickAsync();

        // Nothing on the page shows a number - verify the inferred week against the API.
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var household = await (await http.GetAsync($"/api/households/{me.GetProperty("householdId").GetGuid()}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var kristina = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Kristina");

        var budget = kristina.GetProperty("weeklyTimeBudgetMinutes");
        Assert.Equal(60, budget.GetProperty("monday").GetInt32());
        Assert.Equal(60, budget.GetProperty("saturday").GetInt32());
    }

    [Fact]
    public async Task Changing_the_normal_week_persists_across_a_reload()
    {
        var page = await _app.NewPageAsync();

        await SignUpAsync(page, "Cecilia");

        // The day-by-day editor is an advanced fallback for weeks a role does not fit - see
        // Choosing_a_role_sets_a_whole_week_without_a_single_daily_choice for the primary flow.
        await page.GetByText("Anpassa varje dag för sig").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ändra din vanliga vecka" }).ClickAsync();
        // No number field: a qualitative level picker per day - see Support.TimeLevel.
        await page.Locator(".field-day", new() { HasText = "Mån" })
            .GetByRole(AriaRole.Button, new() { Name = "Gott om tid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Spara vanlig vecka" }).ClickAsync();

        // The editor closes on save, going back to the button that opens it.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ändra din vanliga vecka" }))
            .ToBeVisibleAsync();

        await page.ReloadAsync();
        await page.GetByText("Anpassa varje dag för sig").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ändra din vanliga vecka" }).ClickAsync();

        // The chosen level comes back highlighted, without ever showing a minute count.
        await Assertions.Expect(page.Locator(".field-day", new() { HasText = "Mån" })
            .GetByRole(AriaRole.Button, new() { Name = "Gott om tid" }))
            .ToHaveClassAsync(new Regex("btn-primary"));
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
