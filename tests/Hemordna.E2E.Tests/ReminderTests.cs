using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Egna tider en medlem vill bli påmind om - docs/PRODUCT.md §11. Privata: en
/// påminnelse tillhör bara den som skapade den, syns aldrig för resten av hushållet, och räknas
/// aldrig in i någons tidsbudget eller "N av M klara".</summary>
[Collection(HemordnaAppCollection.Name)]
public class ReminderTests
{
    private readonly HemordnaAppFixture _app;

    public ReminderTests(HemordnaAppFixture app) => _app = app;

    private static async Task<HttpClient> AuthorizedHttpAsync(IPage page, string apiUrl)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    /// <summary>Assumes the page is already on Min dag ("/") - "Ny påminnelse" is one of the
    /// always-visible chips there, same as "Extra uppgift".</summary>
    private static async Task CreateReminderViaUiAsync(
        IPage page, string title, DateOnly date, string? time = null, string? location = null,
        int? travelMinutes = null)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Ny påminnelse" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Ny påminnelse" });
        await sheet.GetByLabel("Titel").FillAsync(title);
        await sheet.GetByLabel("Datum").FillAsync(date.ToString("yyyy-MM-dd"));

        if (time is not null)
        {
            await sheet.GetByLabel("Klockslag").FillAsync(time);
        }

        if (travelMinutes is not null)
        {
            await sheet.GetByLabel("Restid").FillAsync(travelMinutes.Value.ToString());
        }

        if (location is not null)
        {
            await sheet.GetByLabel("Plats").FillAsync(location);
        }

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        // SaveReminderAsync only closes the sheet once its own server round trip (and the
        // LoadDayAsync reload that follows) has actually completed - waiting for that here means
        // a caller that immediately does a real page reload afterwards (proving persistence, not
        // just local state) cannot race ahead of the save and abort the in-flight request.
        await Assertions.Expect(sheet).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Creating_a_reminder_with_a_time_shows_it_at_the_top_of_min_dag_after_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Alva");

        using var http = await AuthorizedHttpAsync(page, _app.ApiUrl);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        // Real household work scheduled too, so "överst, före Rutiner" below is a genuine
        // position check against an actual task row - not just "the only group that exists".
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = AppDate.Today;
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();
        await CreateReminderViaUiAsync(page, "Läkarbesök", today, "14:00");

        // A real reload, not just the same component instance - proves it actually persisted
        // server-side rather than only living in the sheet's own local state.
        await page.ReloadAsync();

        var reminderGroup = page.Locator("ul[aria-label=\"Påminnelser\"]");
        var reminderRow = reminderGroup.GetByText("Läkarbesök");
        await Assertions.Expect(reminderRow).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroup).ToContainTextAsync("14:00");

        var taskRow = page.Locator(".task-name", new() { HasText = "Diska" }).First;
        await Assertions.Expect(taskRow).ToBeVisibleAsync();

        var reminderBox = await reminderGroup.BoundingBoxAsync();
        var taskBox = await taskRow.BoundingBoxAsync();
        Assert.NotNull(reminderBox);
        Assert.NotNull(taskBox);
        Assert.True(reminderBox!.Y < taskBox!.Y, "Påminnelser ska ligga överst, före det vanliga arbetet.");

        // Ingen kryssruta, ingen minutsiffra - bara raden ovan finns, inget "Markera ... som
        // klar"-tillstånd för en påminnelse.
        await Assertions.Expect(
            page.GetByRole(AriaRole.Button, new() { Name = "Markera Läkarbesök som klar" })).ToHaveCountAsync(0);
    }

    /// <summary>The number a member actually acts on - "13:30", not the appointment's own
    /// "14:00" - docs/PRODUCT.md §11. Reloads for real afterwards, same as the first test above,
    /// so this proves the travel time round-tripped through the server rather than only living
    /// in the sheet's own local state.</summary>
    [Fact]
    public async Task Adding_travel_minutes_shows_the_departure_time_on_the_row_after_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        var today = AppDate.Today;
        await CreateReminderViaUiAsync(page, "Tandläkare", today, "14:00", travelMinutes: 30);

        await page.ReloadAsync();

        var reminderGroup = page.Locator("ul[aria-label=\"Påminnelser\"]");
        await Assertions.Expect(reminderGroup.GetByText("Tandläkare")).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroup).ToContainTextAsync("14:00");
        await Assertions.Expect(reminderGroup).ToContainTextAsync("Gå 13:30");
    }

    [Fact]
    public async Task A_reminder_is_never_visible_to_another_member_of_the_same_household()
    {
        var ownerName = "Bea-" + Guid.NewGuid().ToString("N")[..6];
        var ownerPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(ownerPage, ownerName);

        var today = AppDate.Today;
        await CreateReminderViaUiAsync(ownerPage, "Tandläkare", today, "09:00");
        await Assertions.Expect(ownerPage.GetByText("Tandläkare")).ToBeVisibleAsync();

        await ownerPage.GotoAsync("/hushall");
        await ownerPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var inviteDialog = ownerPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        var code = inviteDialog.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var inviteCode = await code.InnerTextAsync();
        await inviteDialog.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        var joinerName = "Cleo-" + Guid.NewGuid().ToString("N")[..6];
        var joinerPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(joinerPage, joinerName);
        await joinerPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await joinerPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await joinerPage.Locator("h1", new() { HasText = joinerName }).WaitForAsync(new() { Timeout = 15_000 });

        // Not on the joiner's own Min dag - no "Påminnelser" group at all, since it only renders
        // when there is at least one, and this member has none of their own.
        await Assertions.Expect(joinerPage.GetByText("Tandläkare")).Not.ToBeVisibleAsync();
        await Assertions.Expect(joinerPage.Locator("ul[aria-label=\"Påminnelser\"]")).ToHaveCountAsync(0);

        // Not in the joiner's own Vecka either.
        await joinerPage.GotoAsync("/vecka");
        await Assertions.Expect(joinerPage.GetByText("Tandläkare")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Cancelling_a_reminder_offers_undo_and_undo_brings_it_back()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Disa");

        var today = AppDate.Today;
        await CreateReminderViaUiAsync(page, "Frisör", today);

        var reminderGroup = page.Locator("ul[aria-label=\"Påminnelser\"]");
        await Assertions.Expect(reminderGroup.GetByText("Frisör")).ToBeVisibleAsync();

        await reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Avboka" }).ClickAsync();

        // The reminder's own group disappears (it was the only one), and a brief "Ångra" offer
        // shows - same shape as UndoBar's own offer after completing a task.
        await Assertions.Expect(page.Locator("ul[aria-label=\"Påminnelser\"]")).ToHaveCountAsync(0);
        var undoRow = page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Avbokat: Frisör" });
        await Assertions.Expect(undoRow).ToBeVisibleAsync();

        await undoRow.GetByRole(AriaRole.Button, new() { Name = "Ångra" }).ClickAsync();

        await Assertions.Expect(undoRow).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("ul[aria-label=\"Påminnelser\"]").GetByText("Frisör")).ToBeVisibleAsync();
    }
}
