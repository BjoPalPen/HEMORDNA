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
        await page.GetByLabel("Uppskattad tid (minuter)").FillAsync("20");
        await page.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await page.GetByLabel("Ansvaret roterar mellan hushållets medlemmar").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa uppgift" }).ClickAsync();

        var row = page.Locator(".list-item", new() { HasText = "Diska" });
        await row.WaitForAsync();
        await Assertions.Expect(row).ToContainTextAsync("varje vecka");
        await Assertions.Expect(row).ToContainTextAsync("roterar");
        await Assertions.Expect(row).ToContainTextAsync("20 min");
    }

    [Fact]
    public async Task A_task_without_a_name_is_rejected_without_calling_the_server()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Bjorn");

        await page.GotoAsync("/uppgifter");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Uppgifter" }).WaitForAsync();

        await page.GetByLabel("Uppskattad tid (minuter)").FillAsync("10");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa uppgift" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("behöver ett namn");
    }
}
