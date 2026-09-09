using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// "Det jag gör i förväg märks, och jag kan se hur" - the single "Tid i förväg: N min" line on
/// Vecka (see docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i
/// förväg"). Deliberately just one number and one sentence - see GetMemberTimeCredit.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class TimeCreditTests
{
    private readonly HemordnaAppFixture _app;

    public TimeCreditTests(HemordnaAppFixture app) => _app = app;

    private static async Task<(HttpClient Http, Guid HouseholdId, Guid MemberId)> ArrangeAsync(
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

        return (http, householdId, memberId);
    }

    [Fact]
    public async Task A_fresh_member_with_no_credit_never_shows_the_line_at_all()
    {
        var page = await _app.NewPageAsync();
        await ArrangeAsync(page, _app.ApiUrl, "Klara");

        await page.GotoAsync("/vecka");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka" }).WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(page.GetByText("Tid i förväg")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Completing_an_extra_task_earns_credit_shown_on_vecka()
    {
        var page = await _app.NewPageAsync();
        var (http, householdId, memberId) = await ArrangeAsync(page, _app.ApiUrl, "Love");

        // "ExtraTask" credit (see CompleteTaskOccurrence) - completing a task added through the
        // "Extra uppgift" flow earns credit regardless of its own scheduled date, unlike
        // "WorkedAhead" credit which depends on completing something before its own due date.
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Extra stadning", estimatedMinutes = 25 })).Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId, addedAsExtra = true }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var complete = await http.PostAsync(
            $"/api/households/{householdId}/occurrences/{occurrence.GetProperty("id").GetGuid()}/complete",
            content: null);
        Assert.True(complete.IsSuccessStatusCode);

        await page.GotoAsync("/vecka");
        await Assertions.Expect(page.GetByText("Tid i förväg: 25 min")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }
}
