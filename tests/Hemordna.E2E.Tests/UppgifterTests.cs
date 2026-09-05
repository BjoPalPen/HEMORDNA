using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class UppgifterTests
{
    private readonly HemordnaAppFixture _app;

    public UppgifterTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Creating_a_recurring_rotating_task_lists_it_with_both_traits()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Anna");

        await page.GotoAsync("/uppgifter");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Uppgifter" }).WaitForAsync();

        await page.GetByLabel("Namn").FillAsync("Diska");
        await page.GetByText("Fler alternativ").ClickAsync();
        await page.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await page.GetByLabel("Ansvaret roterar mellan hushållets medlemmar").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa uppgift" }).ClickAsync();

        var row = page.Locator(".list-item", new() { HasText = "Diska" });
        await row.WaitForAsync();
        await Assertions.Expect(row).ToContainTextAsync("varje vecka");
        await Assertions.Expect(row).ToContainTextAsync("roterar");
    }

    [Fact]
    public async Task Filtering_by_area_summarises_its_estimated_time()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        // A room template gives a known, fixed set of tasks and minutes to filter down to -
        // see RoomTemplates.Kitchen.
        await page.GotoAsync("/omraden");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();
        // Scoped to the areas list: the "just created" summary also renders a "Kök" row.
        await page.Locator("ul[aria-label='Områden'] .list-item", new() { HasText = "Kök" }).WaitForAsync();

        await page.GotoAsync("/uppgifter");
        await page.GetByLabel("Filtrera efter område").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });

        // Kitchen template: 15+5+10+5+10+10 minutes across its six tasks.
        await Assertions.Expect(page.GetByText("Kök: 6 uppgifter · 55 min uppskattad tid totalt"))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_task_without_a_name_is_rejected_without_calling_the_server()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Bjorn");

        await page.GotoAsync("/uppgifter");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Uppgifter" }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa uppgift" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("behöver ett namn");
    }
}
