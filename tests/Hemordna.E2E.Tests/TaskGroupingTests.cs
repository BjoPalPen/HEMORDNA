using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskGroupingTests
{
    private readonly HemordnaAppFixture _app;

    public TaskGroupingTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Todays_tasks_are_grouped_by_room_with_overdue_items_pulled_out_separately()
    {
        // The actual product concern this addresses: DailyPlanner's own sort interleaves rooms
        // (it has no concept of "room" at all) - "Dammsug golvet" (Kök), "Byt handdukar"
        // (Badrum) and "Torka golvet" (Kök) land in that literal order given equal minutes and
        // priority, which reads as a random jumble rather than "finish the kitchen, then the
        // bathroom". The room-grouping this test verifies re-clusters an already-decided list
        // for display only - it does not change what DailyPlanner selected.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { date = today, availableMinutes = 60 });

        var kitchenId = await CreateAreaAsync(http, householdId, "Kök");
        var bathroomId = await CreateAreaAsync(http, householdId, "Badrum");

        // Created in an order deliberately different from the room grouping we expect, so a
        // pass here cannot be an accident of creation order either.
        var vacuumId = await CreateTaskAsync(http, householdId, "Dammsug golvet", kitchenId);
        var towelsId = await CreateTaskAsync(http, householdId, "Byt handdukar", bathroomId);
        var mopId = await CreateTaskAsync(http, householdId, "Torka golvet", kitchenId);
        var overdueId = await CreateTaskAsync(http, householdId, "Diska", kitchenId);

        await ScheduleAsync(http, householdId, vacuumId, today, memberId);
        await ScheduleAsync(http, householdId, towelsId, today, memberId);
        await ScheduleAsync(http, householdId, mopId, today, memberId);
        // Still outstanding from yesterday - genuinely overdue by today.
        await ScheduleAsync(http, householdId, overdueId, today.AddDays(-1), memberId);

        await page.GotoAsync("/");

        var overdueGroup = page.Locator("ul[aria-label=\"Sedan tidigare\"]");
        var kitchenGroup = page.Locator("ul[aria-label=\"Kök\"]");
        var bathroomGroup = page.Locator("ul[aria-label=\"Badrum\"]");

        // The overdue kitchen task is pulled into its own leading section, not the Kök group -
        // an already-late task must never be buried inside a room's list further down.
        await Assertions.Expect(overdueGroup.GetByText("Diska")).ToBeVisibleAsync();
        await Assertions.Expect(kitchenGroup.GetByText("Diska")).Not.ToBeVisibleAsync();

        // Both of today's Kök tasks are together in ONE group, despite DailyPlanner's own flat
        // order interleaving them with the Badrum task in between.
        await Assertions.Expect(kitchenGroup.GetByText("Dammsug golvet")).ToBeVisibleAsync();
        await Assertions.Expect(kitchenGroup.GetByText("Torka golvet")).ToBeVisibleAsync();
        await Assertions.Expect(kitchenGroup.GetByText("Byt handdukar")).Not.ToBeVisibleAsync();

        await Assertions.Expect(bathroomGroup.GetByText("Byt handdukar")).ToBeVisibleAsync();
        await Assertions.Expect(bathroomGroup.GetByText("Dammsug golvet")).Not.ToBeVisibleAsync();

        // Within the room, vacuuming still sorts before mopping (ChoreSequenceHint).
        var kitchenTaskNames = await kitchenGroup.Locator(".task-name").AllTextContentsAsync();
        var vacuumIndex = kitchenTaskNames.ToList().FindIndex(name => name.Contains("Dammsug golvet"));
        var mopIndex = kitchenTaskNames.ToList().FindIndex(name => name.Contains("Torka golvet"));
        Assert.True(vacuumIndex >= 0 && mopIndex >= 0 && vacuumIndex < mopIndex);
    }

    private static async Task<Guid> CreateAreaAsync(HttpClient http, Guid householdId, string name)
    {
        var area = await (await http.PostAsJsonAsync($"/api/households/{householdId}/areas", new { name }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return area.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient http, Guid householdId, string name, Guid areaId)
    {
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name, estimatedMinutes = 5, areaId }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return task.GetProperty("id").GetGuid();
    }

    private static Task ScheduleAsync(HttpClient http, Guid householdId, Guid taskId, DateOnly date, Guid memberId)
        => http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date, assignToMemberId = memberId });
}
