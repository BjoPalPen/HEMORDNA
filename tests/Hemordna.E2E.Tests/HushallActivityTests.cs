using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HushallActivityTests
{
    private readonly HemordnaAppFixture _app;

    public HushallActivityTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Completing_a_task_shows_it_in_the_householders_recent_activity()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Vattna blommorna", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var occurrenceId = occurrence.GetProperty("id").GetGuid();

        var completed = await http.PostAsync(
            $"/api/households/{householdId}/occurrences/{occurrenceId}/complete", content: null);
        completed.EnsureSuccessStatusCode();

        await page.GotoAsync("/hushall");

        var row = page.Locator(".list-item", new() { HasText = "Vattna blommorna" });
        await Assertions.Expect(row).ToBeVisibleAsync();
        await Assertions.Expect(row).ToContainTextAsync("Karin");
    }
}
