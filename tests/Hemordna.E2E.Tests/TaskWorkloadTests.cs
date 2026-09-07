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

        await page.GotoAsync("/omraden");
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        var kitchenCard = page.Locator(".card")
            .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Kök", Exact = true }) });
        await kitchenCard.WaitForAsync();

        await kitchenCard.GetByText("Lägg till en uppgift i Kök").ClickAsync();
        await kitchenCard.GetByLabel("Namn").FillAsync("Diska");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync(); // 15 min
        await kitchenCard.GetByLabel("Upprepning").SelectOptionAsync("Daily");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        // A daily 15-minute task is ~105 min/week (15 * 7) - very different from the flat,
        // frequency-blind "Totalt: ... min" figure, which would only ever show 15.
        await Assertions.Expect(page.GetByText("Ungefär 105 min/vecka")).ToBeVisibleAsync();

        // A fresh household's creator starts at zero weekly capacity (see CreateHousehold) -
        // so any real task should immediately flag as more than the household can cover yet.
        await Assertions.Expect(page.GetByText("Ert hushålls samlade veckokapacitet är 0 min.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("mindre än det uppskattade behovet")).ToBeVisibleAsync();
    }
}
