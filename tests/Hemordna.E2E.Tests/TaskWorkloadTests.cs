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
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync(); // 15 min
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync("Daily");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Diska" }).WaitForAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // A daily 15-minute task is ~105 min/week (15 * 7) - very different from the flat,
        // frequency-blind "Totalt: ... min" figure, which would only ever show 15.
        await Assertions.Expect(page.GetByText("Ungefär 105 min/vecka")).ToBeVisibleAsync();

        // A fresh household's creator starts at zero weekly capacity (see CreateHousehold) -
        // so any real task should immediately flag as more than the household can cover yet.
        await Assertions.Expect(page.GetByText("Ert hushålls samlade veckokapacitet är 0 min.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("mindre än det uppskattade behovet")).ToBeVisibleAsync();
    }
}
