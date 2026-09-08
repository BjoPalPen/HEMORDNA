using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class ExtraTaskTests
{
    private readonly HemordnaAppFixture _app;

    public ExtraTaskTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Adding_an_extra_task_puts_it_on_todays_list_immediately()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        // A fresh household's creator starts at zero minutes a day (see CreateHousehold), so
        // this only lands on today's list - rather than silently under "till en annan dag" -
        // if AddExtraTaskAsync's availability bump actually worked.
        await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();
        await page.GetByLabel("Namn").FillAsync("Rensa garderoben");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lagom tid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till för i dag" }).ClickAsync();

        // Only a task on today's actual (not "till en annan dag") list gets this button.
        var completeButton = page.GetByRole(AriaRole.Button, new() { Name = "Markera Rensa garderoben som klar" });
        await Assertions.Expect(completeButton).ToBeVisibleAsync();

        await completeButton.ClickAsync();
        var row = page.Locator(".task-done", new() { HasText = "Rensa garderoben" });
        await Assertions.Expect(row).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Extra_uppgift_offers_a_pick_list_grouped_by_room()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nils");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        // Two candidates in different rooms, plus one with no room at all ("Övrigt") - a pass
        // here proves the pick list is actually grouped, not just a flat list with a room chip
        // per row (product feedback: a flat list got long and hard to scan).
        var area = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Tvättstuga" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Vik tvätt", estimatedMinutes = 15, areaId = area.GetProperty("id").GetGuid() });
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Rensa garderoben", estimatedMinutes = 30 });

        // Already on today's list - must NOT show up again in the pick list.
        var alreadyToday = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Redan planerad", estimatedMinutes = 10 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { date = today, availableMinutes = 10 });
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{alreadyToday.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Extra uppgift" });

        var tvattstugaGroup = sheet.Locator("ul[aria-label=\"Tvättstuga\"]");
        var ovrigtGroup = sheet.Locator("ul[aria-label=\"Övrigt\"]");

        var candidateRow = tvattstugaGroup.GetByRole(AriaRole.Button, new() { Name = "Vik tvätt" });
        await Assertions.Expect(candidateRow).ToContainTextAsync("Lagom tid");
        await Assertions.Expect(ovrigtGroup.GetByRole(AriaRole.Button, new() { Name = "Rensa garderoben" }))
            .ToContainTextAsync("Lång tid");
        await Assertions.Expect(sheet.GetByText("Redan planerad")).Not.ToBeVisibleAsync();

        await candidateRow.ClickAsync();

        // Landed on today's actual list, reusing its own name/time - no form was ever filled in.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Markera Vik tvätt som klar" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Picking_a_candidate_adds_exactly_that_task_under_its_own_rooms_heading_on_idag()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        async Task<Guid> CreateAreaAsync(string name)
        {
            var area = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/areas", new { name })).Content.ReadFromJsonAsync<JsonElement>();
            return area.GetProperty("id").GetGuid();
        }

        // Three candidates across three different rooms - picking the MIDDLE one is the real
        // test of correct binding, not just "the only one" or "the first one".
        var kitchenId = await CreateAreaAsync("Kök");
        var bathroomId = await CreateAreaAsync("Badrum");
        var hallId = await CreateAreaAsync("Hall");
        await http.PostAsJsonAsync($"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5, areaId = kitchenId });
        await http.PostAsJsonAsync($"/api/households/{householdId}/tasks",
            new { name = "Skölj golvet", estimatedMinutes = 5, areaId = bathroomId });
        await http.PostAsJsonAsync($"/api/households/{householdId}/tasks",
            new { name = "Torka trappsteg", estimatedMinutes = 5, areaId = hallId });

        var taskCountBefore = (await (await http.GetAsync($"/api/households/{householdId}/tasks"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Extra uppgift" });

        await sheet.Locator("ul[aria-label=\"Badrum\"]")
            .GetByRole(AriaRole.Button, new() { Name = "Skölj golvet" }).ClickAsync();

        // Exactly the clicked task landed on Idag - under Badrum's own heading, not Kök's or
        // Hall's, and neither of the other two candidates was added alongside it.
        var bathroomGroup = page.Locator("ul[aria-label=\"Badrum\"]");
        await Assertions.Expect(bathroomGroup.GetByText("Skölj golvet")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".task", new() { HasText = "Diska" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".task", new() { HasText = "Torka trappsteg" })).Not.ToBeVisibleAsync();

        // No duplicate TaskDefinition was created - scheduling an existing candidate must reuse
        // it, never spawn a second copy under the same name.
        var taskCountAfter = (await (await http.GetAsync($"/api/households/{householdId}/tasks"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();
        Assert.Equal(taskCountBefore, taskCountAfter);
    }

    [Fact]
    public async Task Writing_a_brand_new_task_with_a_room_selected_lands_under_that_rooms_heading_on_idag()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        await http.PostAsJsonAsync($"/api/households/{householdId}/areas", new { name = "Städskrubb" });

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();

        // No existing candidates yet, so the plain top-level write-a-new-task form is shown
        // directly (no "Eller skriv en ny uppgift" disclosure to open first).
        await page.GetByLabel("Namn").FillAsync("Torka av hyllor");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lagom tid" }).ClickAsync();
        await page.GetByLabel("Vilket rum?").SelectOptionAsync(new SelectOptionValue { Label = "Städskrubb" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till för i dag" }).ClickAsync();

        // Landed under Städskrubb's own heading on Idag, not lumped into "Övrigt".
        var stadskrubbGroup = page.Locator("ul[aria-label=\"Städskrubb\"]");
        await Assertions.Expect(stadskrubbGroup.GetByText("Torka av hyllor")).ToBeVisibleAsync();
    }
}
