using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Alltid på en viss veckodag" (Björns krav) - see docs/ARCHITECTURE.md "Beslut: Alltid
/// på en viss veckodag".</summary>
[Collection(HemordnaAppCollection.Name)]
public class AlwaysOnWeekdayTests
{
    private readonly HemordnaAppFixture _app;

    public AlwaysOnWeekdayTests(HemordnaAppFixture app) => _app = app;

    private static async Task<(ILocator Room, IPage Page)> ArrangeRoomWithTaskAsync(
        IPage page, string memberName, string roomName, string taskName, string recurrence)
    {
        await SignUpHelper.SignUpAsync(page, memberName);

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync(roomName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = roomName }).First.ClickAsync();
        var room = page.GetByRole(AriaRole.Dialog, new() { Name = roomName });
        await room.WaitForAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = $"Lägg till uppgift i {roomName}" });
        await addSheet.GetByLabel("Namn").FillAsync(taskName);
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync();
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync(recurrence);
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = taskName }).WaitForAsync();

        return (room, page);
    }

    [Fact]
    public async Task Locking_a_weekly_task_to_a_weekday_survives_a_reload()
    {
        var page = await _app.NewPageAsync();
        var (room, _) = await ArrangeRoomWithTaskAsync(page, "Rasmus", "Kök", "Diska", "Weekly");

        await room.GetByRole(AriaRole.Button, new() { Name = "Diska" }).ClickAsync();
        var taskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Diska" });

        var alltidPaRow = taskSheet.GetByRole(AriaRole.Button, new() { Name = "Alltid på" });
        await Assertions.Expect(alltidPaRow).ToContainTextAsync("Ingen särskild dag");

        await alltidPaRow.ClickAsync();
        await taskSheet.GetByLabel("Alltid på").SelectOptionAsync("Wednesday");
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await Assertions.Expect(taskSheet.Locator("select")).Not.ToBeVisibleAsync();

        await Assertions.Expect(taskSheet.GetByRole(AriaRole.Button, new() { Name = "Alltid på" }))
            .ToContainTextAsync("Onsdag");

        // A real reload, not just re-opening the same component instance, to prove it round-
        // tripped through the API.
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        await page.ReloadAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var reopenedRoom = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await reopenedRoom.WaitForAsync();
        await reopenedRoom.GetByRole(AriaRole.Button, new() { Name = "Diska" }).ClickAsync();
        var reopenedTaskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Diska" });
        await Assertions.Expect(reopenedTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Alltid på" }))
            .ToContainTextAsync("Onsdag");
    }

    /// <summary>Daily and "vid behov" tasks have no single weekday to choose - the row must not
    /// even be offered, not merely disabled. See TaskOptionsSheet.HasWeekdayToChoose.</summary>
    [Fact]
    public async Task A_daily_task_never_shows_the_always_on_row()
    {
        var page = await _app.NewPageAsync();
        var (room, _) = await ArrangeRoomWithTaskAsync(page, "Sara", "Badrum", "Torka handfat", "Daily");

        await room.GetByRole(AriaRole.Button, new() { Name = "Torka handfat" }).ClickAsync();
        var taskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Torka handfat" });

        await Assertions.Expect(taskSheet.GetByRole(AriaRole.Button, new() { Name = "Alltid på" })).Not.ToBeVisibleAsync();
    }
}
