using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Jag börjar nu" (Sju enkla lösningar, del 2) - a per-device, per-day marker for the
/// single task someone is currently working on (Support/StartedTask.cs).</summary>
[Collection(HemordnaAppCollection.Name)]
public class StartedTaskTests
{
    private readonly HemordnaAppFixture _app;

    public StartedTaskTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Starting_a_task_shows_pagar_moves_it_first_survives_a_reload_and_clears_on_completion()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Alva");

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
            $"/api/households/{householdId}/areas", new { name = "Kök" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var areaId = area.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Diska", "Damma", "Dammsuga"];

        foreach (var name in names)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name, estimatedMinutes = 5, areaId }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        await page.ReloadAsync();

        var room = page.Locator("ul.task-list[aria-label='Kök']");
        var startHereChips = page.Locator(".chip-today");
        await Assertions.Expect(startHereChips).ToHaveCountAsync(1);

        // Start the LAST task, not the one "Börja här" already points at - only that proves the
        // row actually moves, rather than merely staying where it already was.
        var thirdRow = room.Locator(".task", new() { HasText = "Dammsuga" });
        await thirdRow.Locator(".task-expand").ClickAsync();

        // TaskListItem.razor's expanded ".task-details" is a SIBLING <li>, not a descendant of
        // ".task" - the button lives outside thirdRow's own subtree, so it is found page-wide
        // (only one row is ever expanded at a time, same convention MinDagDetailTests uses).
        await page.GetByRole(AriaRole.Button, new() { Name = "Jag börjar nu" }).ClickAsync();

        await Assertions.Expect(thirdRow.Locator(".chip-primary")).ToHaveTextAsync("Pågår");
        // Something is now under way, so the planner's own "start here" pointer stands down.
        await Assertions.Expect(startHereChips).ToHaveCountAsync(0);

        var firstRowName = room.Locator(".task-name").First;
        await Assertions.Expect(firstRowName).ToContainTextAsync("Dammsuga");

        // Persists across a reload - it lives in localStorage, not just in-memory state.
        await page.ReloadAsync();
        await Assertions.Expect(room.Locator(".chip-primary")).ToHaveTextAsync("Pågår");
        await Assertions.Expect(room.Locator(".task-name").First).ToContainTextAsync("Dammsuga");

        // Completing the started task clears the marker - "Pågår" disappears (the row itself
        // moves to "Klart idag"), and "Börja här" returns to whichever task is now first.
        await room.Locator(".task", new() { HasText = "Dammsuga" }).Locator(".task-check").ClickAsync();
        await Assertions.Expect(page.Locator(".chip-primary")).Not.ToBeVisibleAsync();
        await Assertions.Expect(startHereChips).ToHaveCountAsync(1);
    }
}
