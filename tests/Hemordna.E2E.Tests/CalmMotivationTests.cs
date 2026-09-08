using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B3 - "Lugn" (Installningar.razor) actually shows a
/// phrase now, chosen deterministically by state rather than being a dormant preference.</summary>
[Collection(HemordnaAppCollection.Name)]
public class CalmMotivationTests
{
    private readonly HemordnaAppFixture _app;

    public CalmMotivationTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Calm_with_half_the_days_tasks_done_shows_the_most_important_is_done_phrase()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Yara");

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
            new { presentation = "Text", motivation = "Calm", showTimeLevel = false });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var diska = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{diska.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        var damma = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Damma", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var dammaOccurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{damma.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();

        // One of two done - the "half or more done" rule, not the "everything done" one.
        await http.PostAsync(
            $"/api/households/{householdId}/occurrences/{dammaOccurrence.GetProperty("id").GetGuid()}/complete", null);

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("1 av 2 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).ToHaveTextAsync("Det viktigaste är gjort.");
    }

    [Fact]
    public async Task None_shows_no_phrase_at_all()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Yara");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("0 av 1 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).Not.ToBeVisibleAsync();
    }
}
