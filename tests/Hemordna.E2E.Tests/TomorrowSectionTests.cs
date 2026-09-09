using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// "Jag ser min morgondag på Idag" - the collapsed "Imorgon" section at the bottom of Idag (see
/// docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i förväg").
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class TomorrowSectionTests
{
    private readonly HemordnaAppFixture _app;

    public TomorrowSectionTests(HemordnaAppFixture app) => _app = app;

    private static async Task<(HttpClient Http, Guid HouseholdId, Guid MemberId, DateOnly Today)> ArrangeAsync(
        IPage page, string apiUrl, string displayName)
    {
        await SignUpHelper.SignUpAsync(page, displayName);

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        // Local, not UTC - matches whatever the page's own TimeProvider.GetLocalNow() resolves
        // "Today" to, so the seeded data lands on the exact date the UI itself will look at.
        var today = DateOnly.FromDateTime(DateTime.Now);

        return (http, householdId, memberId, today);
    }

    [Fact]
    public async Task With_nothing_due_tomorrow_the_section_says_so_once_expanded()
    {
        var page = await _app.NewPageAsync();
        await ArrangeAsync(page, _app.ApiUrl, "Ester");

        await page.GotoAsync("/");
        var summary = page.Locator(".tomorrow-section summary");
        await Assertions.Expect(summary).ToHaveTextAsync("Imorgon", new() { Timeout = 15_000 });

        await summary.ClickAsync();
        await Assertions.Expect(page.GetByText("Inget planerat imorgon än.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_task_due_tomorrow_shows_in_the_section_with_a_count()
    {
        var page = await _app.NewPageAsync();
        var (http, householdId, memberId, today) = await ArrangeAsync(page, _app.ApiUrl, "Filip");

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Vattna blommorna", estimatedMinutes = 5 })).Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today.AddDays(1), assignToMemberId = memberId });

        await page.GotoAsync("/");
        var summary = page.Locator(".tomorrow-section summary");
        await Assertions.Expect(summary).ToContainTextAsync("1 uppgifter", new() { Timeout = 15_000 });

        await summary.ClickAsync();
        await Assertions.Expect(page.GetByText("Vattna blommorna")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Gor_idag_i_stallet_moves_the_task_to_today_and_it_leaves_the_tomorrow_section()
    {
        var page = await _app.NewPageAsync();
        var (http, householdId, memberId, today) = await ArrangeAsync(page, _app.ApiUrl, "Gerd");

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Dammsug hallen", estimatedMinutes = 10 })).Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today.AddDays(1), assignToMemberId = memberId });

        await page.GotoAsync("/");
        var summary = page.Locator(".tomorrow-section summary");
        await Assertions.Expect(summary).ToContainTextAsync("1 uppgifter", new() { Timeout = 15_000 });
        await summary.ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Gör idag i stället" }).ClickAsync();

        // On today's own list now, with the "brought forward" chip - and gone from "Imorgon",
        // not duplicated in both places (the exact regression caught during F1's own build:
        // DailyPlanner's "due by this date or earlier" eligibility rule means an occurrence
        // moved to today would otherwise still show up when tomorrow's plan is fetched too).
        var todayRow = page.Locator(".task", new() { HasText = "Dammsug hallen" }).First;
        await Assertions.Expect(todayRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(todayRow.GetByText("I förväg")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Inget planerat imorgon än.")).ToBeVisibleAsync();
    }
}
