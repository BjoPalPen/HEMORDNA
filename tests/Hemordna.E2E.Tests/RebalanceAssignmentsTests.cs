using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class RebalanceAssignmentsTests
{
    private readonly HemordnaAppFixture _app;

    public RebalanceAssignmentsTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Clicking_rebalance_moves_outstanding_work_toward_each_members_own_capacity_share()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var elinId = me.GetProperty("memberId").GetGuid();

        // Elin: 35 min every day (245/week) - Sven: 65 min every day (455/week). The exact 7:13
        // capacity split the new AdultFullTime/Retired presets produce.
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{elinId}/weekly-budget",
            new { monday = 35, tuesday = 35, wednesday = 35, thursday = 35, friday = 35, saturday = 35, sunday = 35 });

        var sven = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/members",
            new { displayName = "Sven", weeklyTimeBudgetMinutes = new { monday = 65, tuesday = 65, wednesday = 65, thursday = 65, friday = 65, saturday = 65, sunday = 65 } }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var svenId = sven.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Five rotating tasks, all currently assigned to Elin by scheduling each one by hand -
        // heavily skewed her way despite her much smaller share of the household's capacity.
        for (var i = 0; i < 5; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks",
                new { name = $"Uppgift {i}", estimatedMinutes = 20, hasRotatingResponsibility = true }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var taskId = task.GetProperty("id").GetGuid();

            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{taskId}/occurrences",
                new { date = today.AddDays(i), assignToMemberId = elinId });
        }

        // "Känns det som att en person gör för mycket?" moved from Rum to Hushåll - see
        // docs/ARCHITECTURE.md "Ny form".
        await page.GotoAsync("/hushall");
        await page.GetByText("Känns det som att en person gör för mycket?").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" }).ClickAsync();

        await Assertions.Expect(page.GetByText("bytte ansvarig")).ToBeVisibleAsync();

        // Sven now has at least one of the five occurrences on one of their five scheduled
        // dates - proof something actually moved off Elin, who has far less than her fair share
        // of the household's capacity to spare.
        var svenItemCount = 0;

        for (var i = 0; i < 5; i++)
        {
            var plan = await (await http.GetAsync(
                $"/api/households/{householdId}/members/{svenId}/plan?date={today.AddDays(i):yyyy-MM-dd}"))
                .Content.ReadFromJsonAsync<JsonElement>();
            svenItemCount += plan.GetProperty("items").GetArrayLength();
        }

        Assert.True(svenItemCount > 0);
    }
}
