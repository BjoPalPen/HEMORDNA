using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>A description written one line per step (Support/TaskSteps.cs) renders as a numbered
/// list in the expanded row - docs/DESIGN.md §6.</summary>
[Collection(HemordnaAppCollection.Name)]
public class TaskStepsTests
{
    private readonly HemordnaAppFixture _app;

    public TaskStepsTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task A_multiline_description_renders_as_a_numbered_list_without_a_leading_marker_left_in()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        // A fresh household's creator starts at zero weekly capacity (see CreateHousehold) - a
        // scheduled task otherwise lands under "Flytta till en annan dag" rather than today's
        // own list, same setup MinDagDetailTests already needs for the same reason.
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new
            {
                name = "Skura golvet",
                estimatedMinutes = 15,
                description = "Ta fram hinken\nFyll med varmt vatten\n- Torka"
            }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        var row = page.Locator(".task", new() { HasText = "Skura golvet" });
        await row.Locator(".task-expand").ClickAsync();

        var steps = page.Locator("ol.task-steps li");
        await Assertions.Expect(steps).ToHaveCountAsync(3);
        await Assertions.Expect(steps.Nth(0)).ToHaveTextAsync("Ta fram hinken");
        await Assertions.Expect(steps.Nth(1)).ToHaveTextAsync("Fyll med varmt vatten");
        await Assertions.Expect(steps.Nth(2)).ToHaveTextAsync("Torka");
    }

    [Fact]
    public async Task A_single_line_description_still_renders_as_a_plain_paragraph()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Signe");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        // A fresh household's creator starts at zero weekly capacity (see CreateHousehold) - a
        // scheduled task otherwise lands under "Flytta till en annan dag" rather than today's
        // own list, same setup MinDagDetailTests already needs for the same reason.
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5, description = "Vik och lägg undan." }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        var row = page.Locator(".task", new() { HasText = "Diska" });
        await row.Locator(".task-expand").ClickAsync();

        await Assertions.Expect(page.GetByText("Vik och lägg undan.")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("ol.task-steps")).Not.ToBeVisibleAsync();
    }
}
