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

        // The plain, template-free area form is a fallback for groupings that are not a room -
        // see Creating_a_room_from_a_template_generates_its_checklist for the primary flow.
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Tvättstuga" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Creating_a_room_from_a_template_generates_its_checklist()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "David");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden" }).WaitForAsync();

        await page.GetByLabel("Typ av rum").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa rum med uppgifter" }).ClickAsync();

        // The name field was left blank, so the room takes the template's own label.
        var areaRow = page.Locator(".list-item", new() { HasText = "Litet wc" });
        await areaRow.WaitForAsync();
        await Assertions.Expect(areaRow).ToContainTextAsync("6 uppgifter");

        await page.GotoAsync("/uppgifter");
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Rengör toalettstolen" }))
            .ToBeVisibleAsync();
    }
}
