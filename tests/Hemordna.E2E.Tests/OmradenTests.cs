using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class OmradenTests
{
    private readonly HemordnaAppFixture _app;

    public OmradenTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Adding_an_area_lists_it_immediately()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden" }).WaitForAsync();

        await page.GetByLabel("Nytt område").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Tvättstuga" }))
            .ToBeVisibleAsync();
    }
}
