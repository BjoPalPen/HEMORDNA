using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Pausing a single room (e.g. a renovation) - see docs/ARCHITECTURE.md "Beslut: Pausa
/// ett rum". Unlike pausing a household or member, which only ever affects what gets generated
/// next, pausing a room also clears what is already outstanding for it - nobody can stand in for
/// a room nobody can use.</summary>
[Collection(HemordnaAppCollection.Name)]
public class RoomPauseTests
{
    private readonly HemordnaAppFixture _app;

    public RoomPauseTests(HemordnaAppFixture app) => _app = app;

    private static ILocator Sheet(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    [Fact]
    public async Task Pausing_a_room_clears_its_outstanding_task_from_idag()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

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
            $"/api/households/{householdId}/areas", new { name = "Badrum" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var areaId = area.GetProperty("id").GetGuid();

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Skrubba dusch", estimatedMinutes = 10, areaId }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        // Scoped to .task-list, not a bare page-wide text search - MinDag.razor always keeps a
        // second, hidden copy of the day in a .print-only block (see "Skriv ut"), which would
        // make a plain Not.ToBeVisibleAsync() pass regardless of whether the real row is gone.
        var taskList = page.Locator(".task-list");
        await page.ReloadAsync();
        await Assertions.Expect(taskList.GetByText("Skrubba dusch")).ToBeVisibleAsync();

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Badrum" }).First.ClickAsync();
        var room = Sheet(page, "Badrum");
        await room.WaitForAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Rummets meny" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Pausa rummet" }).ClickAsync();
        await room.GetByLabel("Pausa till och med").FillAsync(today.AddDays(7).ToString("yyyy-MM-dd"));
        await room.GetByRole(AriaRole.Button, new() { Name = "Pausa rummet" }).ClickAsync();

        await Assertions.Expect(room.GetByText("Rummet är pausat till och med")).ToBeVisibleAsync();

        // The already-outstanding occurrence was skipped the instant the room was paused - not
        // just excluded from future generation, see PauseArea. Wait for the app to actually
        // finish booting first (the h1 greeting) - a bare Not.ToBeVisibleAsync() right after
        // GotoAsync can pass vacuously while Blazor is still loading, regardless of whether the
        // fix works, since "not rendered yet" satisfies "not visible" just as well as "correctly
        // hidden" does.
        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Otto" }).WaitForAsync();
        await Assertions.Expect(page.Locator(".task-list").GetByText("Skrubba dusch")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Resuming_a_paused_room_is_reflected_when_reopening_its_sheet()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Petra");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        await Sheet(page, "Nytt rum").GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await Sheet(page, "Nytt rum").GetByLabel("Rummets namn").FillAsync("Källare");
        await Sheet(page, "Nytt rum").GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await Sheet(page, "Nytt rum").GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Källare" }).First.ClickAsync();
        var room = Sheet(page, "Källare");
        await room.WaitForAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await room.GetByRole(AriaRole.Button, new() { Name = "Rummets meny" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Pausa rummet" }).ClickAsync();
        await room.GetByLabel("Pausa till och med").FillAsync(today.AddDays(7).ToString("yyyy-MM-dd"));
        await room.GetByRole(AriaRole.Button, new() { Name = "Pausa rummet" }).ClickAsync();
        await Assertions.Expect(room.GetByText("Rummet är pausat till och med")).ToBeVisibleAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Återuppta nu" }).ClickAsync();
        await Assertions.Expect(room.GetByText("Rummet är pausat till och med")).Not.ToBeVisibleAsync();

        // Reload and reopen to confirm the resume actually persisted, not just an optimistic
        // client-side flag.
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Källare" }).First.ClickAsync();
        room = Sheet(page, "Källare");
        await room.WaitForAsync();
        await Assertions.Expect(room.GetByText("Rummet är pausat till och med")).Not.ToBeVisibleAsync();
    }
}
