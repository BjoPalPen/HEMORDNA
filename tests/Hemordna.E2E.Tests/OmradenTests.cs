using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>The household's own tasks, straight from the API - for asserting on scheduling
    /// details (which weekday a task lands on) that the UI itself never displays.</summary>
    private async Task<JsonElement> FetchTasksAsync(IPage page)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        return await (await http.GetAsync($"/api/households/{householdId}/tasks")).Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string? WeekdayOf(JsonElement tasks, string name)
        => tasks.EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == name)
            .GetProperty("recurrence").GetProperty("weekday").GetString();

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
    public async Task Shows_a_running_total_across_every_room()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Hedda");

        await page.GotoAsync("/omraden");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Områden", Exact = true }).WaitForAsync();

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();
        await AreaCard(page, "Litet wc").WaitForAsync();

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();
        await AreaCard(page, "Kök").WaitForAsync();

        // Not just the two rooms' own totals (17 + 28) - a household-wide sum shown once,
        // above the room list, so the answer to "how much time is this whole setup?" does not
        // require adding up every card by hand.
        await Assertions.Expect(page.GetByText("Totalt: 12 uppgifter · 45 min")).ToBeVisibleAsync();
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

        // Bedroom template: 2+1+5+3+3+5+5+10 = 34 minutes, repeated for each of the three rooms.
        var summary = page.Locator(".notice", new() { HasText = "Skapat, uppskattad tid per rum" });
        await Assertions.Expect(summary.Locator(".list-item", new() { HasText = "Sovrum 1" }))
            .ToContainTextAsync("34 min");
        await Assertions.Expect(summary).ToContainTextAsync("Totalt: 102 min");
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
    public async Task A_bedroom_templates_window_washing_task_is_restricted_to_adults()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Lovisa");

        await page.GotoAsync("/omraden");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        // Ladders and more care than most chores here - kept off children's rotation by
        // default (see RoomTemplates.Bedroom), unlike the room's everyday tasks.
        var card = AreaCard(page, "Sovrum");
        var windowRow = card.Locator(".list-item", new() { HasText = "Tvätta fönster" });
        await windowRow.WaitForAsync();
        await Assertions.Expect(windowRow).ToContainTextAsync("endast vuxna");

        await Assertions.Expect(card.Locator(".list-item", new() { HasText = "Bädda sängen" }))
            .Not.ToContainTextAsync("endast vuxna");
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

    [Fact]
    public async Task Choosing_a_common_household_chore_creates_it_with_its_template_frequency()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        await page.GotoAsync("/omraden");
        await page.GetByText("Lägg till vanliga hushållssysslor").ClickAsync();
        // Nothing is preselected - it varies too much between households (see GeneralTaskTemplates).
        await page.GetByLabel("Rasta hunden").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till valda" }).ClickAsync();

        var row = page.Locator(".list-item", new() { HasText = "Rasta hunden" });
        await row.WaitForAsync();
        await Assertions.Expect(row).ToContainTextAsync("varje dag");

        // Only the checked chore was created - the rest of the list is still just suggestions.
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Handla mat" })).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_rooms_weekly_tasks_share_a_weekday_but_a_second_room_lands_on_a_different_one()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Hilda");

        await page.GotoAsync("/omraden");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByText("+ Lägg till fler rum").ClickAsync();
        await page.GetByLabel("Rumstyp").Nth(1).SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        // Selecting the second room's type renders its own checklist, shifting the layout below
        // it - wait for that to settle before the submit click lands on a stable target.
        await page.GetByLabel("Rengör spisen").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();

        await AreaCard(page, "Litet wc").WaitForAsync();
        await AreaCard(page, "Kök").WaitForAsync();

        var tasks = await FetchTasksAsync(page);

        // Two of "Litet wc"'s own weekly tasks stay together on the same day...
        Assert.Equal(WeekdayOf(tasks, "Torka av handfatet"), WeekdayOf(tasks, "Rengör toalettstolen"));

        // ...but the other room's weekly task lands on a different day entirely.
        Assert.NotEqual(WeekdayOf(tasks, "Torka av handfatet"), WeekdayOf(tasks, "Rengör spisen"));
    }

    [Fact]
    public async Task Rebalancing_spreads_out_two_rooms_whose_weekly_tasks_collided_on_the_same_day()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ivar");

        await page.GotoAsync("/omraden");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa" }).ClickAsync();
        await AreaCard(page, "Litet wc").WaitForAsync();

        // The manual add-a-task form (unlike the room wizard) always anchors a new weekly task
        // to today - simulating the real-world case this feature exists for: tasks added to
        // different rooms over time that happen to collide on the same weekday.
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();
        await AreaCard(page, "Tvättstuga").WaitForAsync();

        // Every room's own "Lägg till en uppgift"-form exists in the DOM at once (just
        // collapsed), so the fields must be scoped to Tvättstuga's card specifically - a bare
        // GetByLabel("Namn") would match all of them at once.
        var tvattstugaCard = AreaCard(page, "Tvättstuga");
        await tvattstugaCard.GetByText("Lägg till en uppgift i Tvättstuga").ClickAsync();
        await tvattstugaCard.GetByLabel("Namn").FillAsync("Byt handdukar");
        await tvattstugaCard.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await tvattstugaCard.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await page.Locator(".list-item", new() { HasText = "Byt handdukar" }).WaitForAsync();

        var beforeTasks = await FetchTasksAsync(page);
        Assert.Equal(WeekdayOf(beforeTasks, "Torka av handfatet"), WeekdayOf(beforeTasks, "Byt handdukar"));

        await page.GetByText("Ser fördelningen skev ut?").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Sprid ut över veckan" }).ClickAsync();
        // Confirms the rebalance actually reported moving something, not just that the button
        // did nothing quietly.
        await Assertions.Expect(page.GetByText("flyttades", new() { Exact = false })).ToBeVisibleAsync();

        var afterTasks = await FetchTasksAsync(page);
        Assert.NotEqual(WeekdayOf(afterTasks, "Torka av handfatet"), WeekdayOf(afterTasks, "Byt handdukar"));
    }
}
