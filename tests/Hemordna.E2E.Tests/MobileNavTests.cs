using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>docs/DESIGN.md §8: the same four destinations, same order, on mobile and desktop -
/// no "Mer" tab. Inställningar and Logga ut are reached from Hushåll instead.</summary>
[Collection(HemordnaAppCollection.Name)]
public class MobileNavTests
{
    private readonly HemordnaAppFixture _app;

    public MobileNavTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_the_same_four_destinations_on_mobile_as_desktop()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Nora");

        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Huvudmeny" });

        foreach (var visible in new[] { "Idag", "Rum", "Vecka", "Hushåll" })
        {
            await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = visible })).ToBeVisibleAsync();
        }

        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Mer" })).Not.ToBeVisibleAsync();

        await nav.GetByRole(AriaRole.Link, new() { Name = "Hushåll" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();

        // The chevron is a decorative SVG icon (Icon.razor), not literal "›" text, since step 4.
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Inställningar" })).ToBeVisibleAsync();
    }
}
