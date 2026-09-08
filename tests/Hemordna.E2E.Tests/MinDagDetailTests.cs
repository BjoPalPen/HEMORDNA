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
    public async Task Grouped_by_room_does_not_repeat_the_room_as_a_chip_and_can_defer_from_the_expanded_row()
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

        // The room heading above already names "Tvättstuga" (see MinDag.razor's RoomGroups) -
        // product feedback that naming the room twice on the same row (heading + chip) read as
        // redundant, so the chip is suppressed here (ShowAreaChip="false") and shown only in
        // "Sedan tidigare", the one place a row has no room heading above it - see
        // Shows_the_area_as_a_chip_for_an_overdue_row_which_has_no_room_heading below.
        await Assertions.Expect(page.Locator(".task-group-heading", new() { HasText = "Tvättstuga" })).ToBeVisibleAsync();
        var row = page.Locator(".task", new() { HasText = "Plocka tvätt" });
        await Assertions.Expect(row.Locator(".chip")).Not.ToBeVisibleAsync();

        await row.Locator(".task-expand").ClickAsync();
        await Assertions.Expect(page.GetByText("Vik och lägg undan.")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Skjut upp till imorgon" }).ClickAsync();

        // Deferred to tomorrow, so it is no longer part of today's plan.
        await Assertions.Expect(page.GetByText("Plocka tvätt")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Shows_the_area_as_a_chip_for_an_overdue_row_which_has_no_room_heading()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Melker");

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

        var area = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Tvättstuga" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Plocka tvätt", estimatedMinutes = 5, areaId = area.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>();

        // Yesterday, still unaddressed - lands in "Sedan tidigare", which has no per-room
        // heading of its own, so the chip is the only thing naming the room on this row.
        var yesterday = today.AddDays(-1);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = yesterday, assignToMemberId = memberId });

        await page.ReloadAsync();

        var row = page.Locator(".task", new() { HasText = "Plocka tvätt" });
        await Assertions.Expect(row.Locator(".chip")).ToHaveTextAsync("Tvättstuga");
    }

    [Fact]
    public async Task An_overdue_rooms_chip_keeps_its_floor_prefix_to_tell_two_same_named_rooms_apart()
    {
        // Real report: two different rooms both named "Hall" (one per floor) both showed just
        // "Hall" in "Sedan tidigare" once the chip there was stripped to match the grouped
        // list's shorter room heading - but "Sedan tidigare" has no floor heading (or any
        // heading) of its own, so stripping the prefix there throws away the only thing telling
        // the two rows apart.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Freja");

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

        async Task ScheduleYesterdaysOverdueTaskAsync(string areaName, string taskName)
        {
            var area = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/areas", new { name = areaName }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks",
                new { name = taskName, estimatedMinutes = 5, areaId = area.GetProperty("id").GetGuid() }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today.AddDays(-1), assignToMemberId = memberId });
        }

        await ScheduleYesterdaysOverdueTaskAsync("Övre plan – Hall", "Torka trappsteg");
        await ScheduleYesterdaysOverdueTaskAsync("Entré plan – Hall", "Dammsug hallen");

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".task", new() { HasText = "Torka trappsteg" }).Locator(".chip"))
            .ToHaveTextAsync("Övre plan – Hall");
        await Assertions.Expect(page.Locator(".task", new() { HasText = "Dammsug hallen" }).Locator(".chip"))
            .ToHaveTextAsync("Entré plan – Hall");
    }
}
