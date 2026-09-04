using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Proves PRODUCT.md §6's requirement end to end with a real browser and a real SignalR
/// connection: a change made through the API - not through this page - shows up on an open
/// "Min dag" without a reload.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class RealtimeSyncTests
{
    private readonly HemordnaAppFixture _app;

    public RealtimeSyncTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Scheduling_a_task_through_the_api_appears_on_an_open_Min_dag_without_reloading()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Hanna");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        // A fresh household starts its creator at zero minutes a week - give Hanna a normal
        // day so a scheduled task actually lands on it instead of the overflow backlog.
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Hej Hanna!" }).WaitForAsync();

        // Give the page's SignalR connection time to finish joining the household group
        // before the change happens, or the push has nothing to reach.
        await page.WaitForTimeoutAsync(1500);

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 15 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var scheduled = await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });
        scheduled.EnsureSuccessStatusCode();

        // No page.ReloadAsync() here - this is the assertion the whole test exists for.
        await Assertions.Expect(page.GetByText("Diska")).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }
}
