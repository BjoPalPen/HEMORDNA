using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Tyngd per uppgift (Lätt/Mellan/Tung) - see docs/ARCHITECTURE.md "Beslut: Tyngd per
/// uppgift".</summary>
[Collection(HemordnaAppCollection.Name)]
public class EffortTests
{
    private readonly HemordnaAppFixture _app;

    public EffortTests(HemordnaAppFixture app) => _app = app;

    private static ILocator Sheet(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

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
    public async Task Setting_a_tasks_effort_in_the_add_form_is_saved_and_shown_back()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Saga");

        var room = await CreateAndOpenBlankRoomAsync(page, "Badrum");

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = Sheet(page, "Lägg till uppgift i Badrum");
        await addSheet.GetByLabel("Namn").FillAsync("Skrubba dusch");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Tung" }).ClickAsync();
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var taskRow = room.GetByRole(AriaRole.Button, new() { Name = "Skrubba dusch" });
        await taskRow.ClickAsync();
        var taskSheet = Sheet(page, "Skrubba dusch");
        await Assertions.Expect(taskSheet.GetByRole(AriaRole.Button, new() { Name = "Tyngd" })).ToContainTextAsync("Tung");
    }

    [Fact]
    public async Task Changing_a_tasks_effort_afterwards_is_saved_and_survives_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elias");

        var room = await CreateAndOpenBlankRoomAsync(page, "Kök");

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = Sheet(page, "Lägg till uppgift i Kök");
        await addSheet.GetByLabel("Namn").FillAsync("Diska");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var taskRow = room.GetByRole(AriaRole.Button, new() { Name = "Diska" });
        // Nothing chosen in the add form defaults to Mellan.
        await Assertions.Expect(taskRow).ToBeVisibleAsync();

        await taskRow.ClickAsync();
        var taskSheet = Sheet(page, "Diska");
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Tyngd" }).ClickAsync();
        await Assertions.Expect(taskSheet.GetByRole(AriaRole.Button, new() { Name = "Mellan" }))
            .ToHaveClassAsync(new Regex("btn-primary"));

        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Lätt" }).ClickAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // Reopen from scratch - a real reload, not just re-clicking, to prove it round-tripped
        // through the API rather than only surviving in the component's own in-memory state.
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var reopenedRoom = Sheet(page, "Kök");
        await reopenedRoom.GetByRole(AriaRole.Button, new() { Name = "Diska" }).ClickAsync();
        var reopenedTaskSheet = Sheet(page, "Diska");
        await reopenedTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Tyngd" }).ClickAsync();
        await Assertions.Expect(reopenedTaskSheet.GetByRole(AriaRole.Button, new() { Name = "Lätt" }))
            .ToHaveClassAsync(new Regex("btn-primary"));
    }
}
