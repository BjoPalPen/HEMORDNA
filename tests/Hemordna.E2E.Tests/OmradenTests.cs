using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class OmradenTests
{
    private readonly HemordnaAppFixture _app;

    public OmradenTests(HemordnaAppFixture app) => _app = app;

    // Every room template name also appears as an <option> inside the wizard's own select, so a
    // plain ".card" + HasText match would ambiguously match the wizard card too. Filtering by an
    // actual <h2> heading (options carry no heading role) finds only the room's own card.
    private static ILocator AreaCard(IPage page, string name)
        => page.Locator(".card").Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true }) });

    [Fact]
    public async Task Adding_an_area_lists_it_immediately()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        // The plain, template-free area form is a fallback for groupings that are not a room -
        // see Creating_a_room_from_a_template_generates_its_checklist for the primary flow.
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        await Assertions.Expect(AreaCard(page, "Tvättstuga")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Creating_a_room_from_a_template_generates_its_checklist()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "David");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        // No floor name and a single room, so the area takes the template's own label. Its
        // checklist is shown inline on its own card now - no separate task page to visit.
        var card = AreaCard(page, "Litet wc");
        await card.WaitForAsync();
        // 2+5+2+2+3+3 minutes across the template's six tasks.
        await Assertions.Expect(card).ToContainTextAsync("6 uppgifter · 17 min");
        await Assertions.Expect(card.Locator(".list-item", new() { HasText = "Rengör toalettstolen" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Asking_for_several_of_a_room_type_numbers_them_and_summarises_the_time()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        // Three bedrooms in one go, instead of repeating the single-room form three times.
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByLabel("Antal").FillAsync("3");
        // Antal only commits on blur (Blazor's number @bind uses onchange, not oninput). Tab
        // away and let the resulting layout shift (two more owner rows) settle before clicking
        // "Skapa" - otherwise the click can land on a now-shifted target mid-reflow.
        await page.Keyboard.PressAsync("Tab");
        await page.GetByLabel("Vems Sovrum 3").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        await Assertions.Expect(AreaCard(page, "Sovrum 1")).ToBeVisibleAsync();
        await Assertions.Expect(AreaCard(page, "Sovrum 2")).ToBeVisibleAsync();
        await Assertions.Expect(AreaCard(page, "Sovrum 3")).ToBeVisibleAsync();

        // Bedroom template: 2+5+3+1+5 = 16 minutes, repeated for each of the three rooms.
        var summary = page.Locator(".notice", new() { HasText = "Skapat, uppskattad tid per rum" });
        await Assertions.Expect(summary.Locator(".list-item", new() { HasText = "Sovrum 1" }))
            .ToContainTextAsync("16 min");
        await Assertions.Expect(summary).ToContainTextAsync("Totalt: 48 min");
    }

    [Fact]
    public async Task Unchecking_a_template_task_excludes_it_from_the_created_room()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        // Tasks are checked by default - unchecking one leaves it out of the room entirely.
        await page.GetByLabel("Putsa spegeln").UncheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        // 17 total minutes minus the excluded task's 2.
        var card = AreaCard(page, "Litet wc");
        await Assertions.Expect(card).ToContainTextAsync("5 uppgifter · 15 min");
        await Assertions.Expect(card.Locator(".list-item", new() { HasText = "Putsa spegeln" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(card.Locator(".list-item", new() { HasText = "Rengör toalettstolen" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Removing_an_individual_task_takes_it_off_the_rooms_list()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        await page.GotoAsync("/omraden");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        var card = AreaCard(page, "Litet wc");
        var mirrorRow = card.Locator(".list-item", new() { HasText = "Putsa spegeln" });
        await mirrorRow.WaitForAsync();

        // This is the actual ask: removing one activity from a room the household already has,
        // not just excluding it up front in the wizard (see Unchecking_a_template_task_...).
        await mirrorRow.GetByRole(AriaRole.Button, new() { Name = "Ta bort" }).ClickAsync();

        await Assertions.Expect(card.Locator(".list-item", new() { HasText = "Putsa spegeln" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(card).ToContainTextAsync("5 uppgifter · 15 min");
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

        var card = AreaCard(page, "Tvättstuga");
        await card.WaitForAsync();

        // A room can have been created by mistake, or the household changed - see Area.Deactivate.
        // The button's accessible name is "Ta bort {rummet}" (its aria-label), not its visible
        // "Ta bort rum" text - aria-label wins over text content for the accessible name.
        await card.GetByRole(AriaRole.Button, new() { Name = "Ta bort" }).ClickAsync();

        await Assertions.Expect(AreaCard(page, "Tvättstuga")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Naming_a_floor_prefixes_each_of_its_rooms()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Fredrik");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        await page.GetByLabel("Våning (valfritt)").FillAsync("Våning 1");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        await Assertions.Expect(AreaCard(page, "Våning 1 – Kök")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Assigning_a_bedroom_to_a_member_gives_them_sole_responsibility()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Johan");

        // A member to own the bedroom - everything else defaults to shared/rotating.
        await page.GotoAsync("/hushall");
        await page.GetByLabel("Namn").FillAsync("Vera");
        await page.Locator("form").GetByRole(AriaRole.Button, new() { Name = "Barn eller ungdom" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" }).ClickAsync();
        await page.Locator(".list-item", new() { HasText = "Vera" }).WaitForAsync();

        await page.GotoAsync("/omraden");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByLabel("Vems sovrum").SelectOptionAsync(new SelectOptionValue { Label = "Vera" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        var card = AreaCard(page, "Sovrum");
        var bedRow = card.Locator(".list-item", new() { HasText = "Bädda sängen" });
        await bedRow.WaitForAsync();
        await Assertions.Expect(bedRow).ToContainTextAsync("Vera");
        await Assertions.Expect(bedRow).Not.ToContainTextAsync("roterar");
    }

    [Fact]
    public async Task Adding_an_as_needed_task_by_hand_labels_it_vid_behov()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ida");

        await page.GotoAsync("/omraden");
        await page.GetByText("Lägg till en uppgift i Övrigt").ClickAsync();
        await page.GetByLabel("Namn").FillAsync("Putsa fönster");
        await page.GetByLabel("Upprepning").SelectOptionAsync("AsNeeded");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var row = page.Locator(".list-item", new() { HasText = "Putsa fönster" });
        await row.WaitForAsync();
        await Assertions.Expect(row).ToContainTextAsync("vid behov");
    }
}
