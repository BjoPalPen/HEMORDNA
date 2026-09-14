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
        // An_overdue_row_sits_under_its_own_rooms_heading below.
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

        // Igår, fortfarande ogjord. Sedan "Beslut: hela dagen i rumsordning" ligger den i sitt
        // EGET rum under rummets rubrik, inte i en egen grupp högst upp - så rummet namnges av
        // rubriken och raden behöver inget chip som upprepar den.
        var yesterday = today.AddDays(-1);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = yesterday, assignToMemberId = memberId });

        await page.ReloadAsync();

        var group = page.Locator("ul[aria-label=\"Tvättstuga\"]");
        await Assertions.Expect(group.GetByText("Plocka tvätt")).ToBeVisibleAsync();

        // Rummet står i rubriken, alltså inte som ett chip på raden - det vore samma ord två
        // gånger direkt under varandra.
        var row = group.Locator(".task", new() { HasText = "Plocka tvätt" });
        await Assertions.Expect(row.Locator("span.chip:not(.chip-today):not(.chip-time):not(.chip-forward):not(.chip-primary)"))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Two_same_named_rooms_on_different_floors_stay_apart_under_their_own_headings()
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

        // Två rum som båda heter "Hall" hålls isär av VÅNINGSRUBRIKEN ovanför, inte av ett
        // prefix på raden. Varje uppgift ligger under sin egen våning och sitt eget rum.
        var upstairs = page.Locator("ul[aria-label=\"Hall\"]").Filter(new() { HasText = "Torka trappsteg" });
        var entrance = page.Locator("ul[aria-label=\"Hall\"]").Filter(new() { HasText = "Dammsug hallen" });

        await Assertions.Expect(upstairs).ToHaveCountAsync(1);
        await Assertions.Expect(entrance).ToHaveCountAsync(1);

        // Och de är inte samma lista - annars hade rummen slagits ihop till ett.
        await Assertions.Expect(upstairs.GetByText("Dammsug hallen")).ToHaveCountAsync(0);

        var floors = (await page.Locator("h2.floor-heading").AllInnerTextsAsync()).ToList();
        Assert.Contains("Övre plan", floors);
        Assert.Contains("Entré plan", floors);
    }

    /// <summary>A real user's own iPhone (Safari/WebKit) screenshot showed the room chip's pill
    /// struck straight through - a real, if lighter, browser difference: Chromium (this suite's
    /// own test browser) never paints an ancestor's line-through into an inline-flex child like
    /// ".chip" in the first place (confirmed directly: the fix below made no visible difference
    /// in a Chromium screenshot, with or without it), so this test cannot actually reproduce or
    /// disprove the original bug - "chip has no line-through" is trivially true here regardless.
    /// ".chip { text-decoration: none }" is kept anyway as the standard, cross-browser-safe fix
    /// for exactly this class of bug (verified correct on the real device, not just reasoned
    /// about) - this test only pins the one fact Chromium actually lets it verify: the row's own
    /// name keeps its strikethrough.</summary>
    [Fact]
    public async Task A_completed_rows_name_is_struck_through()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Torbjörn");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var area = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Kök" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5, areaId = area.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();

        await http.PostAsync($"/api/households/{householdId}/occurrences/{occurrence.GetProperty("id").GetGuid()}/complete", null);
        await page.ReloadAsync();

        var doneRow = page.Locator(".task-list-done .task", new() { HasText = "Diska" });
        await doneRow.WaitForAsync();

        var nameDecoration = await doneRow.Locator(".task-name").EvaluateAsync<string>(
            "el => getComputedStyle(el).textDecorationLine");
        Assert.Contains("line-through", nameDecoration);
    }
}
