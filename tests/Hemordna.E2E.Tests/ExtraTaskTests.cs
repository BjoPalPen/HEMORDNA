using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class ExtraTaskTests
{
    private readonly HemordnaAppFixture _app;

    public ExtraTaskTests(HemordnaAppFixture app) => _app = app;

    private static async Task<HttpClient> AuthorizedHttpAsync(IPage page, string apiUrl)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    private static async Task<JsonElement> MeAsync(HttpClient http)
        => await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Putting_an_existing_task_on_the_calendar_cannot_assign_it_to_another_account_holder()
    {
        // POST /tasks/{id}/occurrences stays open to every member (see
        // docs/ARCHITECTURE.md "Beslut: Vem får ändra vad"), but only ever onto themselves -
        // same "the caller acts as themselves" pattern as /complete and /reopen. An
        // account-less member is the one exception - see the sibling test below.
        var ownerPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(ownerPage, "Rasmus-" + Guid.NewGuid().ToString("N")[..6]);
        var ownerHttp = await AuthorizedHttpAsync(ownerPage, _app.ApiUrl);
        var ownerMe = await MeAsync(ownerHttp);
        var householdId = ownerMe.GetProperty("householdId").GetGuid();
        var ownerMemberId = ownerMe.GetProperty("memberId").GetGuid();

        await ownerPage.GotoAsync("/hushall");
        await ownerPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var dialog = ownerPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        var code = dialog.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var inviteCode = await code.InnerTextAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        var otherPage = await _app.NewPageAsync();
        var otherName = "Sigrid-" + Guid.NewGuid().ToString("N")[..6];
        await SignUpHelper.RegisterAsync(otherPage, otherName);
        await otherPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await otherPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await otherPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await otherPage.Locator("h1", new() { HasText = otherName }).WaitForAsync(new() { Timeout = 15_000 });
        var otherHttp = await AuthorizedHttpAsync(otherPage, _app.ApiUrl);
        var otherMemberId = (await MeAsync(otherHttp)).GetProperty("memberId").GetGuid();

        var task = await (await ownerHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Dammsug", estimatedMinutes = 10 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignedToOther = await ownerHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = otherMemberId });
        Assert.Equal(HttpStatusCode.Forbidden, assignedToOther.StatusCode);

        var assignedToSelf = await ownerHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = ownerMemberId });
        Assert.True(assignedToSelf.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Putting_an_existing_task_on_the_calendar_can_still_assign_it_to_an_account_less_member()
    {
        // The one exception to "only onto yourself" - an account-less member can never sign in
        // to schedule their own work, so someone else in the household must still be able to do
        // it for them. Same exception MemberSelfAccessFilter already makes for personal routes -
        // see docs/ARCHITECTURE.md "Beslut: Vem får ändra vad".
        var ownerPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(ownerPage, "Tove-" + Guid.NewGuid().ToString("N")[..6]);
        var ownerHttp = await AuthorizedHttpAsync(ownerPage, _app.ApiUrl);
        var householdId = (await MeAsync(ownerHttp)).GetProperty("householdId").GetGuid();

        await ownerPage.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(ownerPage, "Ulla", "Vuxen, jobbar heltid");

        var household = await (await ownerHttp.GetAsync($"/api/households/{householdId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var ullaId = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Ulla")
            .GetProperty("id").GetGuid();

        var task = await (await ownerHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Ullas uppgift", estimatedMinutes = 10 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignedToUlla = await ownerHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = ullaId });

        Assert.True(assignedToUlla.IsSuccessStatusCode);
    }

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
    public async Task Extra_uppgift_still_works_for_a_member_who_cannot_manage_the_household()
    {
        // "Extra uppgift" is daily work, not household configuration - see docs/ARCHITECTURE.md
        // "Beslut: Vem får ändra vad". A joiner starts without CanManageHousehold and must still
        // be able to add one to their own day, end to end through the UI.
        var ownerPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(ownerPage, "Petra-" + Guid.NewGuid().ToString("N")[..6]);
        await ownerPage.GotoAsync("/hushall");
        await ownerPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var dialog = ownerPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        var code = dialog.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var inviteCode = await code.InnerTextAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        var joinerPage = await _app.NewPageAsync();
        var joinerName = "Quentin-" + Guid.NewGuid().ToString("N")[..6];
        await SignUpHelper.RegisterAsync(joinerPage, joinerName);
        await joinerPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await joinerPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await joinerPage.Locator("h1", new() { HasText = joinerName }).WaitForAsync(new() { Timeout = 15_000 });

        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).ClickAsync();
        await joinerPage.GetByLabel("Namn").FillAsync("Diska för hand");
        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Lagom tid" }).ClickAsync();
        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Lägg till för i dag" }).ClickAsync();

        var completeButton = joinerPage.GetByRole(AriaRole.Button, new() { Name = "Markera Diska för hand som klar" });
        await Assertions.Expect(completeButton).ToBeVisibleAsync();
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
