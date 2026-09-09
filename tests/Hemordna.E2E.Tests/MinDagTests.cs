using System.Net.Http.Json;
using System.Text.Json;
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

        // The greeting word depends on time of day (see docs/DESIGN.md "Idag") - only the name
        // itself, in the page's own h1, is what this test can assert regardless of when it runs.
        await Assertions.Expect(page.Locator("h1", new() { HasText = "Anna" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_empty_day_says_so_calmly_and_never_scolds()
    {
        var page = await _app.NewPageAsync();

        await SignUpAsync(page, "Bjorn");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ledigt idag" })).ToBeVisibleAsync();

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

    /// <summary>"Börja här" (Sju enkla lösningar, del 3) - a quiet pointer at the planner's own
    /// first task, list view only, for someone who does not know where to start.</summary>
    [Fact]
    public async Task Exactly_one_row_says_borja_har_and_it_moves_to_the_next_task_once_the_first_is_done()
    {
        var page = await _app.NewPageAsync();
        await SignUpAsync(page, "Wilma");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Diska", "Damma", "Dammsuga"];

        foreach (var name in names)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name, estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        await page.ReloadAsync();

        var startHereChips = page.Locator(".chip-today");
        await Assertions.Expect(startHereChips).ToHaveCountAsync(1);
        await Assertions.Expect(startHereChips).ToHaveTextAsync("Börja här");

        var firstRow = page.Locator(".task", new() { Has = startHereChips });
        var firstCheckButton = firstRow.Locator(".task-check");
        var firstLabel = await firstCheckButton.GetAttributeAsync("aria-label");
        var firstName = names.Single(name => firstLabel == $"Markera {name} som klar");

        await firstCheckButton.ClickAsync();

        // Wait for that specific task's own check button to actually leave the outstanding list
        // (it moves to "Klart idag") rather than just re-checking the chip count, which could
        // already read 1 from the stale, pre-reload DOM and pass without ever having waited.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Markera {firstName} som klar" }))
            .Not.ToBeVisibleAsync();

        // Still more than one outstanding task left, so the chip moves rather than disappears.
        await Assertions.Expect(startHereChips).ToHaveCountAsync(1);
        var secondRow = page.Locator(".task", new() { Has = startHereChips });
        var secondLabel = await secondRow.Locator(".task-check").GetAttributeAsync("aria-label");
        var secondName = names.Single(name => secondLabel == $"Markera {name} som klar");

        Assert.NotEqual(firstName, secondName);
    }

    /// <summary>"Uppdrag: fokuskortet" - a real, deliberate hierarchy instead of five identical
    /// text buttons: exactly two full-width primary actions, "Läs upp" moved to its own 44×44
    /// icon button, "Skjut upp till imorgon"/"Visa nästa" moved down to a quieter escape row.</summary>
    [Fact]
    public async Task Fokuskortet_has_two_primary_buttons_a_44px_speak_button_and_an_escape_row()
    {
        var page = await _app.NewPageAsync();
        await SignUpAsync(page, "Elin");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "OneAtATime", motivation = "None", showTimeLevel = false });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Diska", "Damma"];

        foreach (var name in names)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name, estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        await page.ReloadAsync();
        await page.Locator(".focus-card").WaitForAsync();

        // Only "Bocka av" and "Jag börjar nu" fill .focus-actions now - "Läs upp" and the escape
        // row's two links are no longer among the five identical stacked buttons the bug report
        // showed.
        await Assertions.Expect(page.Locator(".focus-actions .btn-block")).ToHaveCountAsync(2);

        var escape = page.Locator(".focus-escape");
        await Assertions.Expect(escape.GetByRole(AriaRole.Button, new() { Name = "Skjut upp till imorgon" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(escape.GetByRole(AriaRole.Button, new() { Name = "Visa nästa" }))
            .ToBeVisibleAsync();

        var speakBox = await page.GetByRole(AriaRole.Button, new() { Name = "Läs upp" }).BoundingBoxAsync();
        Assert.NotNull(speakBox);
        Assert.InRange(speakBox!.Width, 43, 45);
        Assert.InRange(speakBox.Height, 43, 45);
    }
}
