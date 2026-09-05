using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class OmradenTests
{
    private readonly HemordnaAppFixture _app;

    public OmradenTests(HemordnaAppFixture app) => _app = app;

    // The areas list and the "just created" summary both render .list-item rows with the same
    // room names, so tests need to scope to one or the other rather than search the whole page.
    private static ILocator AreaList(IPage page) => page.Locator("ul[aria-label='Områden']");

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

        await Assertions.Expect(AreaList(page).Locator(".list-item", new() { HasText = "Tvättstuga" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Creating_a_room_from_a_template_generates_its_checklist()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "David");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden" }).WaitForAsync();

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        // No floor name and a single room, so the area takes the template's own label.
        var areaRow = AreaList(page).Locator(".list-item", new() { HasText = "Litet wc" });
        await areaRow.WaitForAsync();
        // 5+10+5+5+5+5 minutes across the template's six tasks.
        await Assertions.Expect(areaRow).ToContainTextAsync("6 uppgifter · 35 min");

        await page.GotoAsync("/uppgifter");
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Rengör toalettstolen" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Asking_for_several_of_a_room_type_numbers_them_and_summarises_the_time()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden" }).WaitForAsync();

        // Three bedrooms in one go, instead of repeating the single-room form three times.
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByLabel("Antal").FillAsync("3");
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        await AreaList(page).Locator(".list-item", new() { HasText = "Sovrum 1" }).WaitForAsync();
        await Assertions.Expect(AreaList(page).Locator(".list-item", new() { HasText = "Sovrum 2" })).ToBeVisibleAsync();
        await Assertions.Expect(AreaList(page).Locator(".list-item", new() { HasText = "Sovrum 3" })).ToBeVisibleAsync();

        // Bedroom template: 5+10+5+5+10 = 35 minutes, repeated for each of the three rooms.
        var summary = page.Locator(".notice", new() { HasText = "Skapat, uppskattad tid per rum" });
        await Assertions.Expect(summary.Locator(".list-item", new() { HasText = "Sovrum 1" }))
            .ToContainTextAsync("35 min");
        await Assertions.Expect(summary).ToContainTextAsync("Totalt: 105 min");
    }

    [Fact]
    public async Task Unchecking_a_template_task_excludes_it_from_the_created_room()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden" }).WaitForAsync();

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        // Tasks are checked by default - unchecking one leaves it out of the room entirely.
        await page.GetByLabel("Putsa spegeln").UncheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        // 35 total minutes minus the excluded task's 5.
        await Assertions.Expect(AreaList(page).Locator(".list-item", new() { HasText = "Litet wc" }))
            .ToContainTextAsync("5 uppgifter · 30 min");

        await page.GotoAsync("/uppgifter");
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Putsa spegeln" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Rengör toalettstolen" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Removing_a_room_takes_it_off_the_list()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Henrietta");

        await page.GotoAsync("/omraden");
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        var row = AreaList(page).Locator(".list-item", new() { HasText = "Tvättstuga" });
        await row.WaitForAsync();

        // A room can have been created by mistake, or the household changed - see Area.Deactivate.
        await row.GetByRole(AriaRole.Button, new() { Name = "Ta bort" }).ClickAsync();

        await Assertions.Expect(AreaList(page).Locator(".list-item", new() { HasText = "Tvättstuga" }))
            .Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Naming_a_floor_prefixes_each_of_its_rooms()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Fredrik");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden" }).WaitForAsync();

        await page.GetByLabel("Våning (valfritt)").FillAsync("Våning 1");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        await Assertions.Expect(AreaList(page).Locator(".list-item", new() { HasText = "Våning 1 – Kök" }))
            .ToBeVisibleAsync();
    }
}
