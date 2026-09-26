using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class OmradenTests
{
    private readonly HemordnaAppFixture _app;

    public OmradenTests(HemordnaAppFixture app) => _app = app;

    /// <summary>BottomSheet.razor gives its shell role="dialog" aria-label="Title" - the room's
    /// own sheet (RoomSheet) and a task's own sheet (TaskOptionsSheet) are both addressable this
    /// way, and only one is normally open at a time.</summary>
    private static ILocator Sheet(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    /// <summary>Opens the "Nytt rum" sheet from the Rum grid - the room-template wizard and the
    /// "tomt rum" fallback both live inside it now (BottomSheet.razor).</summary>
    private static async Task OpenNewRoomSheetAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        await Sheet(page, "Nytt rum").WaitForAsync();
    }

    /// <summary>Opens a room's own sheet from its tile - the tile's accessible name is all of
    /// its text (name, count, badge), so this matches on the room name as a substring.</summary>
    private static async Task OpenRoomAsync(IPage page, string name)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = name }).First.ClickAsync();
        await Sheet(page, name).WaitForAsync();
    }

    private static async Task CloseSheetAsync(ILocator sheet) => await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

    /// <summary>The household's own tasks, straight from the API - for asserting on scheduling
    /// details (which weekday a task lands on) that the UI itself never displays.</summary>
    private async Task<JsonElement> FetchTasksAsync(IPage page)
    {
        var token = await AccessTokenHelper.GetAsync(page, _app.ApiUrl);
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
    public async Task Adding_a_blank_room_lists_it_immediately()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();

        await OpenNewRoomSheetAsync(page);
        // The plain, template-free room form is a fallback for groupings that are not a room -
        // see Creating_a_room_from_a_template_generates_its_checklist for the primary flow.
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Tvättstuga" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Creating_a_room_from_a_template_generates_its_checklist()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "David");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();

        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        // No floor name and a single room, so the area takes the template's own label.
        await OpenRoomAsync(page, "Litet wc");
        var room = Sheet(page, "Litet wc");
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Rengör toalettstolen" }))
            .ToBeVisibleAsync();
        // All six of the template's tasks, one row each.
        await Assertions.Expect(room.Locator(".task-options-row")).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task Shows_a_running_total_across_every_room()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Hedda");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();

        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        // The generic confirmation still describes the first room until this save finishes.
        await Assertions.Expect(Sheet(page, "Nytt rum").GetByRole(AriaRole.List, new() { Name = "Skapade rum" })
            .GetByText("Kök", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(Sheet(page, "Nytt rum").GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }))
            .ToBeEnabledAsync(new() { Timeout = 15_000 });
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        // Not just the two rooms' own totals (17 + 43) - a household-wide sum shown once,
        // above the room grid, so the answer to "how much time is this whole setup?" does not
        // require adding up every room by hand. This total is a flat sum (TotalMinutes),
        // unlike each RoomTile's own "min/v" figure (frequency-weighted, TaskWorkload). Lives
        // behind "Visa tid" now (docs/ARCHITECTURE.md §B5).
        await page.GetByText("Visa tid").ClickAsync();
        await Assertions.Expect(page.GetByText("Totalt: 13 uppgifter · 60 min")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Asking_for_several_of_a_room_type_numbers_them_and_summarises_the_time()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);

        // Three bedrooms in one go, instead of repeating the single-room form three times.
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByLabel("Antal").FillAsync("3");
        // Antal only commits on blur (Blazor's number @bind uses onchange, not oninput). Tab
        // away and let the resulting layout shift (two more owner rows) settle before clicking
        // "Skapa" - otherwise the click can land on a now-shifted target mid-reflow.
        await page.Keyboard.PressAsync("Tab");
        await page.GetByLabel("Vems Sovrum 3").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();

        // Bedroom template: 2+1+8+5+3+3+5+5+10 = 42 minutes, repeated for each of the three rooms.
        var summary = page.Locator(".notice", new() { HasText = "Skapat, uppskattad tid per rum" });
        await Assertions.Expect(summary.Locator(".list-item", new() { HasText = "Sovrum 1" }))
            .ToContainTextAsync("42 min");
        await Assertions.Expect(summary).ToContainTextAsync("Totalt: 126 min");
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sovrum 1" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sovrum 2" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sovrum 3" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Unchecking_a_template_task_excludes_it_from_the_created_room()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);

        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        // Tasks are checked by default - unchecking one leaves it out of the room entirely.
        await page.GetByLabel("Putsa spegeln").UncheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Litet wc");
        var room = Sheet(page, "Litet wc");
        await Assertions.Expect(room.Locator(".task-options-row")).ToHaveCountAsync(5);
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Putsa spegeln" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Rengör toalettstolen" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Removing_an_individual_task_takes_it_off_the_rooms_list()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Litet wc");
        var room = Sheet(page, "Litet wc");

        // This is the actual ask: removing one activity from a room the household already has,
        // not just excluding it up front in the wizard (see Unchecking_a_template_task_...).
        await room.GetByRole(AriaRole.Button, new() { Name = "Putsa spegeln" }).ClickAsync();
        var taskSheet = Sheet(page, "Putsa spegeln");
        await taskSheet.WaitForAsync();
        await taskSheet.GetByRole(AriaRole.Button, new() { Name = "Ta bort uppgiften" }).ClickAsync();

        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Putsa spegeln" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(room.Locator(".task-options-row")).ToHaveCountAsync(5);
    }

    [Fact]
    public async Task Removing_a_room_takes_it_off_the_grid()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Henrietta");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Tvättstuga");
        var room = Sheet(page, "Tvättstuga");

        // A room can have been created by mistake, or the household changed - see Area.Deactivate.
        await room.GetByRole(AriaRole.Button, new() { Name = "Rummets meny" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Ta bort rum" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Tvättstuga" })).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Updated for the Floor rollout (was Naming_a_floor_prefixes_each_of_its_rooms, asserting
    /// the OPPOSITE of what is now correct): a room's own name never carries the floor prefix
    /// any more - Area.Floor is a real, separate field, set alongside the name instead of baked
    /// into it. See Household.AddArea/Area.Floor.
    /// </summary>
    [Fact]
    public async Task Naming_a_floor_creates_the_room_under_it_without_prefixing_its_name()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Fredrik");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);

        await page.GetByLabel("Våning (valfritt)").FillAsync("Våning 1");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        // The tile's accessible name is all of its text (name, count, badge - see OpenRoomAsync's
        // own remarks), so this matches "Kök" as a substring, same as every other room-tile
        // assertion in this file.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Kök" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Våning 1 – Kök" })).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Assigning_a_bedroom_to_a_member_gives_them_sole_responsibility()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Johan");

        // A member to own the bedroom - everything else defaults to shared/rotating.
        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Vera", "Barn eller ungdom");
        await page.GetByRole(AriaRole.Button, new() { Name = "Vera" }).WaitForAsync();

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByLabel("Vems sovrum").SelectOptionAsync(new SelectOptionValue { Label = "Vera" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Sovrum");
        var room = Sheet(page, "Sovrum");
        var bedRow = room.GetByRole(AriaRole.Button, new() { Name = "Bädda sängen" });
        await Assertions.Expect(bedRow).ToContainTextAsync("Vera");
        await Assertions.Expect(bedRow).Not.ToContainTextAsync("roterar");
    }

    [Fact]
    public async Task A_bedroom_templates_window_washing_task_is_restricted_to_adults()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Lovisa");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Sovrum" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        // Ladders and more care than most chores here - kept off children's rotation by
        // default (see RoomTemplates.Bedroom), unlike the room's everyday tasks.
        await OpenRoomAsync(page, "Sovrum");
        var room = Sheet(page, "Sovrum");
        var windowRow = room.GetByRole(AriaRole.Button, new() { Name = "Tvätta fönster" });
        await Assertions.Expect(windowRow).ToContainTextAsync("endast vuxna");

        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Bädda sängen" }))
            .Not.ToContainTextAsync("endast vuxna");
    }

    [Fact]
    public async Task Adding_an_as_needed_task_by_hand_labels_it_vid_behov()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ida");

        await page.GotoAsync("/rum");
        await OpenRoomAsync(page, "Övrigt");
        var room = Sheet(page, "Övrigt");

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = Sheet(page, "Lägg till uppgift i Övrigt");
        await addSheet.GetByLabel("Namn").FillAsync("Putsa fönster");
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync("AsNeeded");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        var row = room.GetByRole(AriaRole.Button, new() { Name = "Putsa fönster" });
        await Assertions.Expect(row).ToContainTextAsync("vid behov");
    }

    [Fact]
    public async Task Choosing_a_common_household_chore_creates_it_with_its_template_frequency()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        await page.GotoAsync("/rum");
        await OpenRoomAsync(page, "Övrigt");
        var room = Sheet(page, "Övrigt");

        await room.GetByText("Lägg till vanliga hushållssysslor").ClickAsync();
        // Nothing is preselected - it varies too much between households (see GeneralTaskTemplates).
        await room.GetByLabel("Rasta hunden").CheckAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till valda" }).ClickAsync();

        var row = room.GetByRole(AriaRole.Button, new() { Name = "Rasta hunden" });
        await Assertions.Expect(row).ToContainTextAsync("varje dag");

        // Only the checked chore was created - the rest of the list is still just suggestions.
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Handla mat" })).Not.ToBeVisibleAsync();
    }

    /// <summary>"Här vill man kunna sätta tid som override" (2026-09-10) - a common chore's
    /// listed minutes are only a starting point, and a real household can need very different
    /// time for the same chore than another - see RoomSheet.razor's EnsureSuggestedMinutes.</summary>
    [Fact]
    public async Task Overriding_a_common_chores_time_before_adding_it_uses_the_chosen_minutes()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Maja");

        await page.GotoAsync("/rum");
        await OpenRoomAsync(page, "Övrigt");
        var room = Sheet(page, "Övrigt");

        await room.GetByText("Lägg till vanliga hushållssysslor").ClickAsync();
        await room.GetByLabel("Handla mat").CheckAsync();

        // The template's own default (45 min) is not itself a selectable level - the closest
        // one (Lång tid, 30 min) is pre-highlighted rather than nothing at all.
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Lång tid" }))
            .ToHaveClassAsync(new Regex("btn-primary"));

        await room.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till valda" }).ClickAsync();

        // The chosen 5 min, not the template's original 45 - "· 5 min", not a bare "5 min",
        // since "45 min" (the template's own default) itself contains "5 min" as a substring.
        var row = room.GetByRole(AriaRole.Button, new() { Name = "Handla mat" });
        await Assertions.Expect(row).ToContainTextAsync("· 5 min");
    }

    /// <summary>"Det räcker inte med dessa gränser... vi kanske måste åka långt för att handla"
    /// (2026-09-10) - a real errand can take hours, not just the old top bucket's 30 minutes -
    /// see Support.TimeLevel and docs/ARCHITECTURE.md "Beslut: Fler tidsnivåer, upp till flera
    /// timmar". A shared scale (TimeLevel.All) - this only exercises it through the suggestions
    /// list, but the same two levels are available everywhere time is chosen.</summary>
    [Fact]
    public async Task A_long_errand_can_be_given_several_hours()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

        await page.GotoAsync("/rum");
        await OpenRoomAsync(page, "Övrigt");
        var room = Sheet(page, "Övrigt");

        await room.GetByText("Lägg till vanliga hushållssysslor").ClickAsync();
        await room.GetByLabel("Handla mat").CheckAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Flera timmar" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till valda" }).ClickAsync();

        var row = room.GetByRole(AriaRole.Button, new() { Name = "Handla mat" });
        await Assertions.Expect(row).ToContainTextAsync("· 120 min");
    }

    /// <summary>"En fundering" (2026-09-10) - laundry is work per LOAD, so a household's real
    /// frequency depends on how many people generate loads, not a single fixed default - see
    /// RoomTemplateTask.FrequencyFor and docs/ARCHITECTURE.md "Beslut: Mallfrekvens skalad efter
    /// hushållsstorlek".</summary>
    [Fact]
    public async Task A_scaling_chores_frequency_matches_the_templates_own_default_for_a_single_member_household()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        await page.GotoAsync("/rum");
        await OpenRoomAsync(page, "Övrigt");
        var room = Sheet(page, "Övrigt");

        await room.GetByText("Lägg till vanliga hushållssysslor").ClickAsync();
        await room.GetByLabel("Tvätta och lägga in tvätt").CheckAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till valda" }).ClickAsync();

        var row = room.GetByRole(AriaRole.Button, new() { Name = "Tvätta och lägga in tvätt" });
        await Assertions.Expect(row).ToContainTextAsync("varje vecka");
    }

    [Fact]
    public async Task A_scaling_chore_speeds_up_for_a_larger_household_while_an_unrelated_chore_does_not()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Lasse");

        var token = await AccessTokenHelper.GetAsync(page, _app.ApiUrl);
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        // Three more members, for four active in total - the household's own creator already
        // counts as the first.
        foreach (var name in new[] { "Ines", "Oskar", "Vera" })
        {
            await http.PostAsJsonAsync($"/api/households/{householdId}/members", new { displayName = name });
        }

        // A fresh load, not an in-app navigation - RoomSheet's Members parameter comes from
        // Rum.razor's own household fetch on init, which must see all four before the sheet
        // opens.
        await page.GotoAsync("/rum");
        await OpenRoomAsync(page, "Övrigt");
        var room = Sheet(page, "Övrigt");

        await room.GetByText("Lägg till vanliga hushållssysslor").ClickAsync();
        await room.GetByLabel("Tvätta och lägga in tvätt").CheckAsync();
        await room.GetByLabel("Vattna växter").CheckAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till valda" }).ClickAsync();

        var laundryRow = room.GetByRole(AriaRole.Button, new() { Name = "Tvätta och lägga in tvätt" });
        await Assertions.Expect(laundryRow).ToContainTextAsync("ungefär var 2:e dag");

        // Watering plants has nothing to do with how many people live there - its own template
        // default is untouched.
        var plantsRow = room.GetByRole(AriaRole.Button, new() { Name = "Vattna växter" });
        await Assertions.Expect(plantsRow).ToContainTextAsync("varje vecka");
    }

    [Fact]
    public async Task A_rooms_weekly_tasks_share_a_weekday_but_a_second_room_lands_on_a_different_one()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Hilda");

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByText("+ Lägg till fler rum").ClickAsync();
        await page.GetByLabel("Rumstyp").Nth(1).SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        // Selecting the second room's type renders its own checklist, shifting the layout below
        // it - wait for that to settle before the submit click lands on a stable target.
        await page.GetByLabel("Rengör spisen").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();

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

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenNewRoomSheetAsync(page);
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Tvättstuga");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Tvättstuga");
        var tvattstuga = Sheet(page, "Tvättstuga");
        await tvattstuga.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = Sheet(page, "Lägg till uppgift i Tvättstuga");
        await addSheet.GetByLabel("Namn").FillAsync("Byt handdukar");
        await addSheet.GetByLabel("Upprepning").SelectOptionAsync("Weekly");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await tvattstuga.GetByRole(AriaRole.Button, new() { Name = "Byt handdukar" }).WaitForAsync();

        // The room wizard now places each new room's weekly tasks itself (see
        // docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen"), so the two rooms created above
        // no longer collide on their own. RebalanceSchedule's real purpose is fixing a household
        // whose tasks predate that placement (or were edited by hand onto the same day) - force
        // that exact situation directly through the API, same as a stale collision would look.
        var beforeTasks = await FetchTasksAsync(page);
        var handdukarId = beforeTasks.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Byt handdukar")
            .GetProperty("id").GetGuid();
        var collisionDay = WeekdayOf(beforeTasks, "Torka av handfatet")!;

        var token = await AccessTokenHelper.GetAsync(page, _app.ApiUrl);
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var today = AppDate.Today;

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/tasks/{handdukarId}/frequency",
            new { recurrence = new { frequency = "Weekly", interval = 1, startDate = today, weekday = collisionDay } });

        var collidedTasks = await FetchTasksAsync(page);
        Assert.Equal(WeekdayOf(collidedTasks, "Torka av handfatet"), WeekdayOf(collidedTasks, "Byt handdukar"));

        await page.GotoAsync("/vecka");
        await page.GetByText("Vill du fördela om dagarna?").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Fördela om dagarna" }).ClickAsync();
        // Confirms the rebalance actually reported moving something, not just that the button
        // did nothing quietly.
        await Assertions.Expect(page.GetByText("flyttades", new() { Exact = false })).ToBeVisibleAsync();

        var afterTasks = await FetchTasksAsync(page);
        Assert.NotEqual(WeekdayOf(afterTasks, "Torka av handfatet"), WeekdayOf(afterTasks, "Byt handdukar"));
    }

    /// <summary>Real bug (2026-09-10): "Övrigt" was only ever rendered while
    /// <c>_selectedFloor is null</c>, but a household where EVERY room happens to have a floor
    /// never gets a null-valued "Annat" tab to click back to null with - Rum.razor's own Floors
    /// getter only includes null when at least one area lacks a floor. "Övrigt" became
    /// permanently unreachable the moment a floor tab was selected, contradicting DESIGN.md's own
    /// "alltid synligt". Updated for the Floor rollout to set Floor as its own field via the API
    /// instead of baking it into the name - a name containing " – " no longer means anything to
    /// Rum.razor's floor tabs, which now read Area.Floor directly.</summary>
    [Fact]
    public async Task Ovrigt_stays_reachable_even_when_every_room_has_a_floor()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Björn");

        var token = await AccessTokenHelper.GetAsync(page, _app.ApiUrl);
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        // Two floors, every single room has a Floor set - no room ever falls into "Annat", so
        // Floors never contains a null entry and the tab bar never offers one to click.
        await http.PostAsJsonAsync($"/api/households/{householdId}/areas", new { name = "Kök", floor = "Övre plan" });
        await http.PostAsJsonAsync($"/api/households/{householdId}/areas", new { name = "Tvättstuga", floor = "Källar plan" });

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Övre plan" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Övrigt" })).ToBeVisibleAsync();
    }

    /// <summary>The owner picker in the creation wizard (RoomOwnerPicker.razor, formerly
    /// BedroomOwnerPicker.razor and hardcoded to the Sovrum template) now shows for every room
    /// template, not just Sovrum - same assertion shape as
    /// Assigning_a_bedroom_to_a_member_gives_them_sole_responsibility, but for Kök.</summary>
    [Fact]
    public async Task Assigning_a_kitchen_to_a_member_at_creation_gives_them_sole_responsibility()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nora");

        // A member to own the kitchen - everything else defaults to shared/rotating.
        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Filip", "Barn eller ungdom");
        await page.GetByRole(AriaRole.Button, new() { Name = "Filip" }).WaitForAsync();

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Kök" });
        // Owner picker's per-instance label lowercases the template's own label for a single
        // instance ("Vems kök"), same convention the bedroom picker already used ("Vems sovrum").
        await page.GetByLabel("Vems kök").SelectOptionAsync(new SelectOptionValue { Label = "Filip" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Kök");
        var room = Sheet(page, "Kök");
        var stoveRow = room.GetByRole(AriaRole.Button, new() { Name = "Rengör spisen" });
        await Assertions.Expect(stoveRow).ToContainTextAsync("Filip");
        await Assertions.Expect(stoveRow).Not.ToContainTextAsync("roterar");
    }

    /// <summary>The after-creation counterpart: RoomSheet's own "Rummets meny" can now set one
    /// owner for every task in an existing room at once (SaveOwnerAsync), the same batch pattern
    /// "Ändra frekvens för hela rummet" already used for frequency - see
    /// docs/ARCHITECTURE.md "Beslut: ett rum, en person, en dag".</summary>
    [Fact]
    public async Task Setting_an_owner_for_a_whole_room_from_its_menu_updates_every_task()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Sixten");

        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Wilma", "Barn eller ungdom");
        await page.GetByRole(AriaRole.Button, new() { Name = "Wilma" }).WaitForAsync();

        await page.GotoAsync("/rum");
        await OpenNewRoomSheetAsync(page);
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Hall" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await CloseSheetAsync(Sheet(page, "Nytt rum"));

        await OpenRoomAsync(page, "Hall");
        var room = Sheet(page, "Hall");
        var shoesRow = room.GetByRole(AriaRole.Button, new() { Name = "Ställ i ordning skorna" });
        await Assertions.Expect(shoesRow).ToContainTextAsync("roterar");

        await room.GetByRole(AriaRole.Button, new() { Name = "Rummets meny" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = "Sätt utförare för hela rummet" }).ClickAsync();
        await room.GetByLabel("Utförare för hela Hall").SelectOptionAsync(new SelectOptionValue { Label = "Wilma" });
        await room.GetByRole(AriaRole.Button, new() { Name = "Spara för alla 4 uppgifter" }).ClickAsync();
        await room.GetByText("Uppdaterade 4 uppgifter.").WaitForAsync();

        // The owner view (like BulkFrequency) stays open after saving - back to the task list
        // to see every row reflect the new owner, not just the one open when the menu was used.
        await room.GetByRole(AriaRole.Button, new() { Name = "Till uppgifterna" }).ClickAsync();
        await Assertions.Expect(shoesRow).ToContainTextAsync("Wilma");
        await Assertions.Expect(shoesRow).Not.ToContainTextAsync("roterar");
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Dammsug eller sopa golvet" }))
            .ToContainTextAsync("Wilma");
        await Assertions.Expect(room.GetByRole(AriaRole.Button, new() { Name = "Släng gammal post och reklam" }))
            .ToContainTextAsync("Wilma");
    }
}
