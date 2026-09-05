using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class PlaneringTests
{
    private readonly HemordnaAppFixture _app;

    public PlaneringTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_a_bar_for_every_weekday()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Johanna");

        await page.GotoAsync("/planering");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min tidsbudget" }).WaitForAsync();

        await Assertions.Expect(page.Locator(".chart-column")).ToHaveCountAsync(7);
        // A fresh household starts its creator at zero minutes a day - see CreateHousehold.
        await Assertions.Expect(page.Locator(".chart-column").First).ToContainTextAsync("0");
    }

    [Fact]
    public async Task Changing_todays_time_updates_the_planned_summary()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Kristina");

        await page.GotoAsync("/planering");
        await page.GetByRole(AriaRole.Button, new() { Name = "Ändra tid idag" }).ClickAsync();
        await page.GetByLabel("Minuter idag").FillAsync("45");
        await page.GetByRole(AriaRole.Button, new() { Name = "Spara för idag" }).ClickAsync();

        await Assertions.Expect(page.GetByText("0 av 45 min planerade idag.")).ToBeVisibleAsync();
    }
}
