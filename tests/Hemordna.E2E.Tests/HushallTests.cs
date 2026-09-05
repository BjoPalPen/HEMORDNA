using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HushallTests
{
    private readonly HemordnaAppFixture _app;

    public HushallTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_the_household_name_and_the_creator_as_a_member()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "David", "Familjen Svensson");

        await page.GotoAsync("/hushall");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Svensson" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "David" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Adding_a_member_shows_their_weekly_total()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Erik");

        await page.GotoAsync("/hushall");
        await page.GetByLabel("Namn").FillAsync("Filippa");
        await page.GetByLabel("Normal tid per dag (minuter, samma varje veckodag)").FillAsync("30");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" }).ClickAsync();

        var row = page.Locator(".list-item", new() { HasText = "Filippa" });
        await row.WaitForAsync();
        // 30 minutes every day of the week is 210 minutes total.
        await Assertions.Expect(row).ToContainTextAsync("210 min/vecka");
    }

    [Fact]
    public async Task Shows_an_areas_task_count()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/omraden");
        await page.GetByLabel("Nytt område").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Kök" })).ToBeVisibleAsync();

        await page.GotoAsync("/uppgifter");
        await page.GetByLabel("Namn").FillAsync("Diska");
        await page.GetByLabel("Område").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa uppgift" }).ClickAsync();
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Diska" })).ToBeVisibleAsync();

        await page.GotoAsync("/hushall");

        var areaRow = page.Locator(".list-item", new() { HasText = "Kök" });
        await Assertions.Expect(areaRow).ToContainTextAsync("1 uppgifter");
    }
}
