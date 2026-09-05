using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>docs/DESIGN.md §8: mobile shows four destinations plus "Mer" for the rest.</summary>
[Collection(HemordnaAppCollection.Name)]
public class MobileNavTests
{
    private readonly HemordnaAppFixture _app;

    public MobileNavTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_four_destinations_and_a_Mer_link_that_lists_the_rest()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Nora");

        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Huvudmeny" });

        foreach (var visible in new[] { "Min dag", "Områden", "Planering", "Mer" })
        {
            await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = visible })).ToBeVisibleAsync();
        }

        foreach (var hidden in new[] { "Hushåll", "Inställningar" })
        {
            await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = hidden })).ToBeHiddenAsync();
        }

        await nav.GetByRole(AriaRole.Link, new() { Name = "Mer" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Mer" }).WaitForAsync();

        foreach (var link in new[] { "Hushåll", "Inställningar" })
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = link })).ToBeVisibleAsync();
        }
    }
}
