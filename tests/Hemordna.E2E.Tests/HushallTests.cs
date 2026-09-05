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
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "David" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Adding_a_member_infers_their_week_from_a_role_without_asking_for_a_number()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Erik");

        await page.GotoAsync("/hushall");
        await page.GetByLabel("Namn").FillAsync("Filippa");
        // No minute field, and not even a per-day choice: one role infers the whole week -
        // see Support.HouseholdRolePresets.
        await page.Locator("form").GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" }).ClickAsync();

        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Filippa" })).ToBeVisibleAsync();

        // Nothing in the UI shows a number, but the role's weekday/weekend split must actually
        // have been sent - verified against the API, the only place minutes still live.
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var household = await (await http.GetAsync($"/api/households/{me.GetProperty("householdId").GetGuid()}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var filippa = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Filippa");

        var budget = filippa.GetProperty("weeklyTimeBudgetMinutes");
        Assert.Equal(30, budget.GetProperty("monday").GetInt32());
        Assert.Equal(60, budget.GetProperty("saturday").GetInt32());
    }

    [Fact]
    public async Task Changing_a_members_role_from_the_household_page_updates_their_week()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia");

        await page.GotoAsync("/hushall");
        // Role management lives on the household page, not on Min dag - that page is not
        // something every member opens daily, unlike their own day. See DESIGN.md §6b.
        var row = page.Locator(".list-item", new() { HasText = "Cecilia" });
        await row.GetByLabel("Roll för Cecilia")
            .SelectOptionAsync(new SelectOptionValue { Label = "Pensionär / hemma dagtid" });

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var household = await (await http.GetAsync($"/api/households/{me.GetProperty("householdId").GetGuid()}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var cecilia = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Cecilia");

        var budget = cecilia.GetProperty("weeklyTimeBudgetMinutes");
        Assert.Equal(60, budget.GetProperty("monday").GetInt32());
        Assert.Equal(60, budget.GetProperty("saturday").GetInt32());

        // The dropdown reflects the saved role back, not just accepts the click.
        await page.ReloadAsync();
        row = page.Locator(".list-item", new() { HasText = "Cecilia" });
        await Assertions.Expect(row.GetByLabel("Roll för Cecilia")).ToHaveValueAsync("Retired");
    }

    [Fact]
    public async Task Removing_a_member_takes_them_off_the_list()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Gustav");

        await page.GotoAsync("/hushall");
        await page.GetByLabel("Namn").FillAsync("Filippa");
        await page.Locator("form").GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" }).ClickAsync();

        var row = page.Locator(".list-item", new() { HasText = "Filippa" });
        await row.WaitForAsync();

        // Someone moved out, or was added by mistake - see HouseholdMember.Deactivate.
        await row.GetByRole(AriaRole.Button, new() { Name = "Ta bort" }).ClickAsync();

        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Filippa" })).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Shows_an_areas_task_count()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        await page.GotoAsync("/omraden");
        // The plain area form is tucked behind a disclosure now that room templates are the
        // primary path - see OmradenTests for that flow.
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        // "Kök" is also a <option> in the wizard's own room-type select, so a card matched by
        // HasText alone would ambiguously catch that card too - filter by the actual heading.
        var kitchenCard = page.Locator(".card")
            .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Kök", Exact = true }) });
        await kitchenCard.WaitForAsync();

        // Task management lives inline on each room's own card now - see OmradenTests.
        await kitchenCard.GetByText("Lägg till en uppgift i Kök").ClickAsync();
        await kitchenCard.GetByLabel("Namn").FillAsync("Diska");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await Assertions.Expect(kitchenCard.Locator(".list-item", new() { HasText = "Diska" })).ToBeVisibleAsync();

        await page.GotoAsync("/hushall");

        var areaRow = page.Locator(".list-item", new() { HasText = "Kök" });
        await Assertions.Expect(areaRow).ToContainTextAsync("1 uppgifter");
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
