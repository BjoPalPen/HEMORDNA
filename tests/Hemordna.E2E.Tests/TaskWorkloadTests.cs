using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskWorkloadTests
{
    private readonly HemordnaAppFixture _app;

    public TaskWorkloadTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Weekly_estimate_weighs_a_daily_task_by_how_often_it_actually_recurs()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nora");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var room = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await room.WaitForAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Lägg till uppgift i Kök" });
        await addSheet.GetByLabel("Namn").FillAsync("Diska");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync(); // 5 min
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync("Daily");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Diska" }).WaitForAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // Time figures live behind "Visa tid" now (docs/ARCHITECTURE.md §B5) - open it once.
        await page.GetByText("Visa tid").ClickAsync();

        // A daily 5-minute task is ~35 min/week (5 * 7) - very different from the flat,
        // frequency-blind "Totalt: ... min" figure, which would only ever show 5.
        await Assertions.Expect(page.GetByText("Ungefär 35 min/vecka")).ToBeVisibleAsync();

        // A fresh household's creator starts at zero weekly capacity (see CreateHousehold) -
        // so any real task should immediately flag as more than the household can cover yet.
        await Assertions.Expect(page.GetByText("Ert hushålls samlade veckokapacitet är 0 min.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("mindre än det uppskattade behovet")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Changing_a_tasks_time_estimate_after_creation_updates_its_row()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var room = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await room.WaitForAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Lägg till uppgift i Kök" });
        await addSheet.GetByLabel("Namn").FillAsync("Diska");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync(); // 5 min
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var taskRow = room.GetByRole(AriaRole.Button, new() { Name = "Diska" });
        await Assertions.Expect(taskRow).ToContainTextAsync("5 min");

        // Time was only ever settable at creation until TaskOptionsSheet grew its own "Tid" row -
        // TaskDefinition.ChangeEstimatedMinutes already existed, unused, same gap steg 3 found
        // for assignment/room/requires-adult.
        await taskRow.ClickAsync();
        var taskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Diska" });
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Tid" }).ClickAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Lång tid" }).ClickAsync(); // 30 min
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await Assertions.Expect(taskRow).ToContainTextAsync("30 min");
        await Assertions.Expect(taskRow).Not.ToContainTextAsync("5 min");
    }

    /// <summary>Regression test: RoomSheet's own _openTask was a snapshot taken when the row was
    /// tapped, never refreshed after TaskOptionsSheet's OnChanged reloaded AllTasks - so a save
    /// went through fine server-side while the still-open sheet's own summary row kept showing
    /// the pre-edit value, reading as if the edit had silently reverted. Checked here without
    /// closing the sheet first, unlike the test above, which is exactly what let this slip
    /// through.</summary>
    [Fact]
    public async Task A_saved_change_shows_immediately_in_the_options_sheet_without_closing_it()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Petra");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var room = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await room.WaitForAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Lägg till uppgift i Kök" });
        await addSheet.GetByLabel("Namn").FillAsync("Diska");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync(); // 5 min
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var taskRow = room.GetByRole(AriaRole.Button, new() { Name = "Diska" });
        await taskRow.ClickAsync();
        var taskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Diska" });

        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Tid" }).ClickAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Lång tid" }).ClickAsync(); // 30 min
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        // Saving returns to the options list straight away, but the picker's own level buttons
        // (also named "... tid") make "Tid" an ambiguous match for the brief instant both are in
        // the DOM - wait for the picker to be gone before resolving the row, rather than racing
        // Assertions.Expect against that transient ambiguity.
        await Assertions.Expect(taskSheet.Locator(".level-picker")).Not.ToBeVisibleAsync();

        // Still on the options list, sheet never closed - the "Tid" row's own summary must
        // reflect the save, not the value from before it (regression: RoomSheet's _openTask was
        // never refreshed after the reload this save triggers, so this used to keep showing the
        // pre-edit value here even though the save itself had already succeeded).
        var tidRow = taskSheet.GetByRole(AriaRole.Button, new() { Name = "Tid" });
        await Assertions.Expect(tidRow).ToContainTextAsync("Lång tid");
        await Assertions.Expect(tidRow).Not.ToContainTextAsync("Lite tid");
    }
}
