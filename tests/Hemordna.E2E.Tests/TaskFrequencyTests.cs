using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskFrequencyTests
{
    private readonly HemordnaAppFixture _app;

    public TaskFrequencyTests(HemordnaAppFixture app) => _app = app;

    private static ILocator Sheet(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    /// <summary>Creates a blank room via the "Nytt rum" sheet and opens it, landing on its own
    /// RoomSheet with the sheet closed behind it.</summary>
    private static async Task<ILocator> CreateAndOpenBlankRoomAsync(IPage page, string name)
    {
        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = Sheet(page, "Nytt rum");
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = name }).First.ClickAsync();
        var room = Sheet(page, name);
        await room.WaitForAsync();
        return room;
    }

    [Fact]
    public async Task Changing_a_tasks_frequency_updates_what_is_shown_without_recreating_it()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Tova");

        var room = await CreateAndOpenBlankRoomAsync(page, "Kök");

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = Sheet(page, "Lägg till uppgift i Kök");
        await addSheet.GetByLabel("Namn").FillAsync("Diska");
        // Default estimate is fine - only the recurrence choice matters for this test.
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync("Daily");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var taskRow = room.GetByRole(AriaRole.Button, new() { Name = "Diska" });
        await Assertions.Expect(taskRow).ToContainTextAsync("varje dag");

        await taskRow.ClickAsync();
        var taskSheet = Sheet(page, "Diska");
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Upprepning" }).ClickAsync();
        await taskSheet.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await Assertions.Expect(taskRow).ToContainTextAsync("varje vecka");
        await Assertions.Expect(taskRow).Not.ToContainTextAsync("varje dag");
    }

    [Fact]
    public async Task Changing_frequency_retires_the_now_stale_outstanding_occurrence()
    {
        // The actual production bug this fixes: moving a task to a new weekday left its old,
        // already-generated occurrence outstanding forever - nagging every day (endlessly
        // deferred) alongside a fresh one generated for the new weekday once it arrived. See
        // docs/ARCHITECTURE.md. Drives the frequency change through the API directly - this is
        // about EnsureOccurrencesGenerated's behaviour, not the UI form that makes the same call.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Wilma");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{me.GetProperty("memberId").GetGuid()}/availability",
            new { date = today, availableMinutes = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new
            {
                name = "Torka golvet",
                estimatedMinutes = 10,
                hasRotatingResponsibility = true,
                recurrence = new { frequency = "Daily", interval = 1, startDate = today }
            }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        await page.GotoAsync("/");
        var completeButton = page.GetByRole(AriaRole.Button, new() { Name = "Markera Torka golvet som klar" });
        await Assertions.Expect(completeButton).ToBeVisibleAsync();

        // Move it to a weekday that is not today, far enough that no fresh occurrence for it is
        // due yet - anything still shown afterwards must be the old, stale one.
        var otherWeekday = today.DayOfWeek == DayOfWeek.Monday ? DayOfWeek.Wednesday : DayOfWeek.Monday;
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/frequency",
            new { recurrence = new { frequency = "Weekly", interval = 1, startDate = today, weekday = otherWeekday.ToString() } });

        await page.ReloadAsync();
        await Assertions.Expect(completeButton).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_interval_can_be_set_for_a_weekly_task_and_is_shown_back()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        var room = await CreateAndOpenBlankRoomAsync(page, "Tvättstuga");

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = Sheet(page, "Lägg till uppgift i Tvättstuga");
        await addSheet.GetByLabel("Namn").FillAsync("Rengör filter");
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await addSheet.GetByLabel("Hur många veckor mellan varje gång?").FillAsync("4");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var taskRow = room.GetByRole(AriaRole.Button, new() { Name = "Rengör filter" });
        await Assertions.Expect(taskRow).ToContainTextAsync("var 4:e vecka");

        // Reopening the edit form shows the interval that was actually saved, not a blank field.
        await taskRow.ClickAsync();
        var taskSheet = Sheet(page, "Rengör filter");
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Upprepning" }).ClickAsync();
        await Assertions.Expect(taskSheet.GetByLabel("Hur många veckor mellan varje gång?")).ToHaveValueAsync("4");
    }

    [Fact]
    public async Task Changing_frequency_for_the_whole_room_updates_every_task_in_it()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karl");

        var room = await CreateAndOpenBlankRoomAsync(page, "Sovrum 2");

        foreach (var name in new[] { "Dammsug golvet", "Vädra rummet" })
        {
            await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
            var addSheet = Sheet(page, "Lägg till uppgift i Sovrum 2");
            await addSheet.GetByLabel("Namn").FillAsync(name);
            await addSheet.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
            await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
            await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = name })).ToBeVisibleAsync();
        }

        await room.GetByRole(AriaRole.Button, new() { Name = "Rummets meny" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Ändra frekvens för hela rummet" }).ClickAsync();
        await room.GetByLabel("Ny upprepning för hela Sovrum 2").SelectOptionAsync("Monthly");
        await room.GetByRole(AriaRole.Button, new() { Name = "Spara för alla 2 uppgifter" }).ClickAsync();

        await Assertions.Expect(room.GetByText("Uppdaterade 2 uppgifter.")).ToBeVisibleAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Till uppgifterna" }).ClickAsync();

        foreach (var name in new[] { "Dammsug golvet", "Vädra rummet" })
        {
            await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = name })).ToContainTextAsync("varje månad");
        }
    }
}
