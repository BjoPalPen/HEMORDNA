using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Covers the row detail docs/DESIGN.md §6 specifies: area chip, and expand to defer.</summary>
[Collection(HemordnaAppCollection.Name)]
public class MinDagDetailTests
{
    private readonly HemordnaAppFixture _app;

    public MinDagDetailTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_the_area_as_a_chip_and_can_defer_from_the_expanded_row()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Lovisa");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var area = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Tvättstuga" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var areaId = area.GetProperty("id").GetGuid();

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Plocka tvätt", estimatedMinutes = 5, areaId, description = "Vik och lägg undan." }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        var row = page.Locator(".task", new() { HasText = "Plocka tvätt" });
        await Assertions.Expect(row.Locator(".chip")).ToHaveTextAsync("Tvättstuga");

        await row.Locator(".task-expand").ClickAsync();
        await Assertions.Expect(page.GetByText("Vik och lägg undan.")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Skjut upp till imorgon" }).ClickAsync();

        // Deferred to tomorrow, so it is no longer part of today's plan.
        await Assertions.Expect(page.GetByText("Plocka tvätt")).Not.ToBeVisibleAsync();
    }
}
