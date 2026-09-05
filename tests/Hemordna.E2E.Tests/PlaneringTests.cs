using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class PlaneringTests
{
    private readonly HemordnaAppFixture _app;

    public PlaneringTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_a_qualitative_row_for_every_weekday()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Johanna");

        await page.GotoAsync("/planering");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka" }).WaitForAsync();

        await Assertions.Expect(page.Locator(".list-item")).ToHaveCountAsync(7);
        // A fresh household starts its creator at zero minutes a day - see CreateHousehold.
        // No number anywhere - "Ingen tid" is the qualitative equivalent.
        await Assertions.Expect(page.Locator(".list-item").First).ToContainTextAsync("Ingen tid");
    }
}
