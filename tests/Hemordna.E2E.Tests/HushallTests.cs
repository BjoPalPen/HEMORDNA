using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HushallTests
{
    private readonly HemordnaAppFixture _app;

    public HushallTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_the_household_name_and_the_creator_as_a_member()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "David", "Familjen Svensson");

        await page.GotoAsync("/hushall");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Svensson" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "David" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Adding_a_member_infers_their_week_from_a_role_without_asking_for_a_number()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Erik");

        await page.GotoAsync("/hushall");
        // No minute field, and not even a per-day choice: one role infers the whole week -
        // see Support.HouseholdRolePresets.
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Filippa", "Vuxen, jobbar heltid");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Filippa" })).ToBeVisibleAsync();

        // Nothing in the UI shows a number, but the role's budget must actually have been sent
        // - verified against the API, the only place minutes still live. AdultFullTime is 35
        // min every day (245/week) - a 7:13 ratio against Retired's 65/day, so the two roles'
        // combined default is a 35%/65% split - see docs/ARCHITECTURE.md "65/35 target split".
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var household = await (await http.GetAsync($"/api/households/{me.GetProperty("householdId").GetGuid()}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var filippa = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Filippa");

        var budget = filippa.GetProperty("weeklyTimeBudgetMinutes");
        Assert.Equal(35, budget.GetProperty("monday").GetInt32());
        Assert.Equal(35, budget.GetProperty("saturday").GetInt32());
    }

    [Fact]
    public async Task Changing_a_members_role_from_the_household_page_updates_their_week()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia");

        await page.GotoAsync("/hushall");
        // Role management lives behind the member's own avatar, not on Idag - that page is not
        // something every member opens daily, unlike their own day. See DESIGN.md §6b.
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Cecilia");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Pensionär / hemma dagtid" }).ClickAsync();

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var household = await (await http.GetAsync($"/api/households/{me.GetProperty("householdId").GetGuid()}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var cecilia = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Cecilia");

        // Retired is 65 min every day (455/week) - see docs/ARCHITECTURE.md "65/35 target split".
        var budget = cecilia.GetProperty("weeklyTimeBudgetMinutes");
        Assert.Equal(65, budget.GetProperty("monday").GetInt32());
        Assert.Equal(65, budget.GetProperty("saturday").GetInt32());

        // The sheet reflects the saved role back (a filled preset button), not just accepts the
        // click - reopen it after a reload and check the preset is highlighted as active.
        await page.ReloadAsync();
        sheet = await HushallHelper.OpenMemberSheetAsync(page, "Cecilia");
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Pensionär / hemma dagtid" }))
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("btn-primary"));
    }

    /// <summary>"Kan familjen själv korrigera budget ex per dag eller mer tid på veckoslut?"
    /// (2026-09-10) - the API already took a full weekday-by-weekday budget (used throughout
    /// the E2E suite's own household seeding), but the UI only ever offered one flat number for
    /// all seven days at once. See docs/ARCHITECTURE.md "Beslut: Anpassa tid per veckodag".</summary>
    [Fact]
    public async Task Setting_a_per_weekday_budget_gives_more_time_on_weekends_and_survives_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nils");

        await page.GotoAsync("/hushall");
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Nils");

        await sheet.GetByText("Anpassa tid per veckodag").ClickAsync();
        await sheet.GetByLabel("Måndag").FillAsync("20");
        await sheet.GetByLabel("Tisdag").FillAsync("20");
        await sheet.GetByLabel("Onsdag").FillAsync("20");
        await sheet.GetByLabel("Torsdag").FillAsync("20");
        await sheet.GetByLabel("Fredag").FillAsync("20");
        await sheet.GetByLabel("Lördag").FillAsync("90");
        await sheet.GetByLabel("Söndag").FillAsync("90");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var household = await (await http.GetAsync($"/api/households/{me.GetProperty("householdId").GetGuid()}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var budget = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Nils")
            .GetProperty("weeklyTimeBudgetMinutes");

        Assert.Equal(20, budget.GetProperty("monday").GetInt32());
        Assert.Equal(20, budget.GetProperty("friday").GetInt32());
        Assert.Equal(90, budget.GetProperty("saturday").GetInt32());
        Assert.Equal(90, budget.GetProperty("sunday").GetInt32());

        // Reflected back, not just accepted - reopen after a reload and confirm the same values
        // pre-fill the form rather than reverting to whatever they were before.
        await page.ReloadAsync();
        sheet = await HushallHelper.OpenMemberSheetAsync(page, "Nils");
        await sheet.GetByText("Anpassa tid per veckodag").ClickAsync();
        await Assertions.Expect(sheet.GetByLabel("Lördag")).ToHaveValueAsync("90");
        await Assertions.Expect(sheet.GetByLabel("Måndag")).ToHaveValueAsync("20");
    }

    [Fact]
    public async Task Removing_a_member_takes_them_off_the_avatar_row()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Gustav");

        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Filippa", "Vuxen, jobbar heltid");

        // Someone moved out, or was added by mistake - see HouseholdMember.Deactivate.
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Filippa");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Ta bort medlem" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Filippa" })).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_new_rooms_task_count_shows_on_its_room_tile()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/rum");
        // The plain room form is tucked behind a disclosure inside "Nytt rum" now that room
        // templates are the primary path - see OmradenTests for that flow.
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // Task management lives in the room's own sheet now - see OmradenTests. The room list
        // that used to live on Hushåll is gone (Rum's own tiles already show this - see
        // docs/DESIGN.md "Rum") - the tile itself is what this test now checks.
        await page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First.ClickAsync();
        var kitchen = page.GetByRole(AriaRole.Dialog, new() { Name = "Kök" });
        await kitchen.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Lägg till uppgift i Kök" });
        await addSheet.GetByLabel("Namn").FillAsync("Diska");
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await Assertions.Expect(kitchen.GetByRole(AriaRole.Button, new() { Name = "Diska" })).ToBeVisibleAsync();
        await kitchen.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Kök" }).First)
            .ToContainTextAsync("1 uppgifter");
    }

    [Fact]
    public async Task Completing_todays_only_task_marks_todays_dot_done()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Henrik");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Häng tvätt", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var occurrenceId = occurrence.GetProperty("id").GetGuid();

        await http.PostAsync($"/api/households/{householdId}/occurrences/{occurrenceId}/complete", content: null);

        await page.GotoAsync("/hushall");

        var row = page.Locator("tbody tr", new() { HasText = "Henrik" });
        // The only task this member has all week is done, so exactly one dot is filled.
        await Assertions.Expect(row.Locator(".dot-done")).ToHaveCountAsync(1);
    }
}
