using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        var token = await AccessTokenHelper.GetAsync(page, apiUrl);
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    /// <summary>Assumes the page is already on Min dag ("/") - "Ny påminnelse" is one of the
    /// always-visible chips there, same as "Extra uppgift". <paramref name="visibilityButtonLabel"/>
    /// is one of the level-picker's three exact button texts ("Bara jag", "Andra ser att jag har
    /// en tid", "Andra ser vad det är") - left <c>null</c> to keep the sheet's own default
    /// ("Bara jag" / Private). <paramref name="audienceButtonLabel"/> is one of the audience
    /// picker's two exact button texts ("Alla i hushållet", "Bara utvalda") - only reachable (and
    /// only meaningful to pass) once <paramref name="visibilityButtonLabel"/> is not "Bara jag".
    /// <paramref name="selectedMemberNames"/> checks exactly those candidates in the "Bara
    /// utvalda" list, by their display name - only meaningful together with
    /// <paramref name="audienceButtonLabel"/>: "Bara utvalda".</summary>
    private static async Task CreateReminderViaUiAsync(
        IPage page, string title, DateOnly date, string? time = null, string? location = null,
        int? travelMinutes = null, string? visibilityButtonLabel = null,
        string? audienceButtonLabel = null, IReadOnlyList<string>? selectedMemberNames = null)
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

        if (visibilityButtonLabel is not null)
        {
            await sheet.GetByRole(AriaRole.Button, new() { Name = visibilityButtonLabel, Exact = true }).ClickAsync();
        }

        if (audienceButtonLabel is not null)
        {
            await sheet.GetByRole(AriaRole.Button, new() { Name = audienceButtonLabel, Exact = true }).ClickAsync();
        }

        foreach (var name in selectedMemberNames ?? [])
        {
            await sheet.GetByLabel(name, new() { Exact = true }).CheckAsync();
        }

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        // SaveReminderAsync only closes the sheet once its own server round trip (and the
        // LoadDayAsync reload that follows) has actually completed - waiting for that here means
        // a caller that immediately does a real page reload afterwards (proving persistence, not
        // just local state) cannot race ahead of the save and abort the in-flight request.
        await Assertions.Expect(sheet).Not.ToBeVisibleAsync();
    }

    /// <summary>Reopens an existing reminder's "Ändra"-sheet (already on Min dag, "/") and
    /// changes only its audience, same wording rules as <see cref="CreateReminderViaUiAsync"/>.
    /// Leaves every other field as it already was - the sheet is pre-filled from the reminder's
    /// current server state (OpenEditReminderSheet), so nothing here needs to repeat title, date
    /// or visibility.</summary>
    private static async Task ChangeReminderAudienceViaUiAsync(
        IPage page, string title, string audienceButtonLabel, IReadOnlyList<string>? selectedMemberNames = null)
    {
        var row = page.Locator("ul[aria-label=\"Påminnelser\"] li", new() { HasText = title });
        await row.GetByRole(AriaRole.Button, new() { Name = "Ändra" }).ClickAsync();

        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Ändra påminnelse" });
        await sheet.GetByRole(AriaRole.Button, new() { Name = audienceButtonLabel, Exact = true }).ClickAsync();

        foreach (var name in selectedMemberNames ?? [])
        {
            await sheet.GetByLabel(name, new() { Exact = true }).CheckAsync();
        }

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await Assertions.Expect(sheet).Not.ToBeVisibleAsync();
    }

    /// <summary>Two account-holding members of the same household - Anna (creator) and Björn
    /// (joined via invite code), same shape as MemberAccessControlTests' own
    /// ArrangeTwoAccountHoldersAsync, but keeping the IPage handles too since these tests assert
    /// on rendered Vecka/Min dag markup, not just HTTP responses.</summary>
    private async Task<(IPage AnnaPage, IPage BjornPage, HttpClient AnnaHttp, HttpClient BjornHttp, Guid HouseholdId)>
        ArrangeTwoMembersAsync()
    {
        var annaPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(annaPage, "Anna-" + Guid.NewGuid().ToString("N")[..6]);

        await annaPage.GotoAsync("/hushall");
        await annaPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var inviteDialog = annaPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        var code = inviteDialog.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var inviteCode = await code.InnerTextAsync();
        await inviteDialog.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // Back to Min dag - ReadInviteCodeAsync-style navigation left Anna on /hushall, but
        // CreateReminderViaUiAsync assumes "Ny påminnelse" is on screen, same as every other
        // test in this file.
        await annaPage.GotoAsync("/");
        await annaPage.GetByRole(AriaRole.Button, new() { Name = "Ny påminnelse" }).WaitForAsync(new() { Timeout = 15_000 });

        var bjornPage = await _app.NewPageAsync();
        var bjornName = "Björn-" + Guid.NewGuid().ToString("N")[..6];
        await SignUpHelper.RegisterAsync(bjornPage, bjornName);
        await bjornPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bjornPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bjornPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bjornPage.Locator("h1", new() { HasText = bjornName }).WaitForAsync(new() { Timeout = 15_000 });

        var annaHttp = await AuthorizedHttpAsync(annaPage, _app.ApiUrl);
        var bjornHttp = await AuthorizedHttpAsync(bjornPage, _app.ApiUrl);
        var me = await (await annaHttp.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        return (annaPage, bjornPage, annaHttp, bjornHttp, householdId);
    }

    /// <summary>Three account-holding members of the same household - Anna (creator), Björn and
    /// Cecilia (both joined via the same invite code). Sibling of
    /// <see cref="ArrangeTwoMembersAsync"/>, not an extension of it: the reminder-audience tests
    /// need a THIRD member who is deliberately never selected, to prove Selected actually
    /// narrows who sees a shared time rather than just "everyone except the owner" - two members
    /// alone cannot tell those two apart.</summary>
    private async Task<(IPage AnnaPage, IPage BjornPage, IPage CeciliaPage, string BjornName, string CeciliaName)>
        ArrangeThreeMembersAsync()
    {
        var annaPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(annaPage, "Anna-" + Guid.NewGuid().ToString("N")[..6]);

        await annaPage.GotoAsync("/hushall");
        await annaPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var inviteDialog = annaPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        var code = inviteDialog.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var inviteCode = await code.InnerTextAsync();
        await inviteDialog.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await annaPage.GotoAsync("/");
        await annaPage.GetByRole(AriaRole.Button, new() { Name = "Ny påminnelse" }).WaitForAsync(new() { Timeout = 15_000 });

        var bjornName = "Björn-" + Guid.NewGuid().ToString("N")[..6];
        var bjornPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(bjornPage, bjornName);
        await bjornPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bjornPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bjornPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bjornPage.Locator("h1", new() { HasText = bjornName }).WaitForAsync(new() { Timeout = 15_000 });

        var ceciliaName = "Cecilia-" + Guid.NewGuid().ToString("N")[..6];
        var ceciliaPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(ceciliaPage, ceciliaName);
        await ceciliaPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await ceciliaPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await ceciliaPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await ceciliaPage.Locator("h1", new() { HasText = ceciliaName }).WaitForAsync(new() { Timeout = 15_000 });

        // Anna's own _household (loaded once, above, right after she read the invite code) is
        // now stale - Björn and Cecilia joined AFTER that load, so her in-memory Members list
        // still only has herself, and the "Bara utvalda" checkbox list (ReminderAudienceCandidates)
        // would offer nobody. A real reload is what makes MinDag.razor's OnInitializedAsync fetch
        // the household again, this time with all three members - see CreateReminderViaUiAsync's
        // audience-picker steps, which assume Björn/Cecilia are already offered as checkboxes.
        await annaPage.ReloadAsync();
        await annaPage.GetByRole(AriaRole.Button, new() { Name = "Ny påminnelse" }).WaitForAsync(new() { Timeout = 15_000 });

        return (annaPage, bjornPage, ceciliaPage, bjornName, ceciliaName);
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

    /// <summary>Nedräkning mot avgång (denna revision, docs/PRODUCT.md §11, Support/
    /// DepartureCountdown.cs) - staplarna och "om N min" visas BREDVID den redan befintliga
    /// "Gå HH:mm"-raden, aldrig i stället för den, när avgången ligger inom en timme från nu.
    /// Klockslaget sätts relativt <c>DateTime.Now</c> (inte <see cref="AppDate.Today"/>, som bara
    /// ger datumet, inte klockslaget) eftersom en verklig tidpunkt nära nu krävs för att hamna
    /// innanför 60-minutersfönstret - lokal tid av samma skäl som AppDate.cs:s egna remarks
    /// beskriver för datumet. travelMinutes är satt till ett positivt tal (5), inte 0 - domänen
    /// kräver ett positivt tal för restid (till skillnad från DepartureCountdownTests egen
    /// MakeReminder-hjälpare, som konstruerar DTO:n direkt och aldrig går via domänvalideringen).
    /// </summary>
    [Fact]
    public async Task A_reminder_departing_within_the_hour_shows_a_countdown()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Freja");

        var today = AppDate.Today;
        const int travelMinutes = 5;
        // Avgång (klockslag minus restid) om ca 20 minuter - gott om marginal på båda sidor om
        // fönstrets gränser (0 och 60 minuter) för att tåla den tid testet självt tar att köra.
        var timeOfDay = DateTime.Now.AddMinutes(20 + travelMinutes).ToString("HH:mm");
        await CreateReminderViaUiAsync(page, "Massage", today, timeOfDay, travelMinutes: travelMinutes);

        var reminderGroup = page.Locator("ul[aria-label=\"Påminnelser\"]");
        await Assertions.Expect(reminderGroup.GetByText("Massage")).ToBeVisibleAsync();

        // Klockslaget står kvar - staplarna kompletterar det, ersätter det inte.
        await Assertions.Expect(reminderGroup).ToContainTextAsync("Gå ");

        var bars = reminderGroup.Locator(".departure-bars");
        await Assertions.Expect(bars).ToBeVisibleAsync();
        await Assertions.Expect(bars).ToHaveAttributeAsync("role", "img");
        var ariaLabel = await bars.GetAttributeAsync("aria-label");
        Assert.NotNull(ariaLabel);
        Assert.Contains("kvar till avgång", ariaLabel);

        // "om N min" bredvid staplarna, från samma Countdown.
        await Assertions.Expect(reminderGroup).ToContainTextAsync(new Regex(@"om \d+ min"));
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

    /// <summary>Björns egentliga begäran: en påminnelse ska gå att bocka av, precis som en
    /// uppgift - utan att försvinna från dagen (till skillnad från en avbokning) och utan att
    /// ge upphov till "vid tiden"-notiser för en tid som redan är hanterad (bevisat på
    /// domän-/applikationsnivå i ReminderNotificationSelectorTests, inte här). En riktig
    /// omladdning bevisar att markeringen faktiskt sparades server-side, inte bara i sheetens
    /// egna lokala tillstånd.</summary>
    [Fact]
    public async Task Checking_off_a_reminder_marks_it_and_the_marking_survives_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Embla");

        var today = AppDate.Today;
        await CreateReminderViaUiAsync(page, "Tandläkare", today, "13:00");

        var reminderGroup = page.Locator("ul[aria-label=\"Påminnelser\"]");
        await Assertions.Expect(reminderGroup.GetByText("Tandläkare")).ToBeVisibleAsync();

        await reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Bocka av" }).ClickAsync();

        // Offers "Ångra", same shape as cancelling - just a different label ("Avbockat", not
        // "Avbokat").
        var undoRow = page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Avbockat: Tandläkare" });
        await Assertions.Expect(undoRow).ToBeVisibleAsync();

        // Still on its day right away - unlike a cancelled reminder, which disappears - with a
        // visible checked marking and no more "Bocka av"/"Ändra"/"Avboka" actions on the row.
        var checkedRow = page.Locator("li.task-done", new() { HasText = "Tandläkare" });
        await Assertions.Expect(checkedRow).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Bocka av" })).ToHaveCountAsync(0);
        await Assertions.Expect(reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Avboka" })).ToHaveCountAsync(0);
        await Assertions.Expect(reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Ändra" })).ToHaveCountAsync(0);

        // A real reload, not just the same component instance - proves the checked-off marking
        // actually persisted server-side (Reminder.CheckOff -> ReminderStatus.CheckedOff) rather than
        // only living in this page's own in-memory state.
        await page.ReloadAsync();

        var reminderGroupAfterReload = page.Locator("ul[aria-label=\"Påminnelser\"]");
        await Assertions.Expect(reminderGroupAfterReload.GetByText("Tandläkare")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("li.task-done", new() { HasText = "Tandläkare" })).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroupAfterReload.GetByRole(AriaRole.Button, new() { Name = "Avboka" })).ToHaveCountAsync(0);
    }

    /// <summary>Synlighet, steg 1 - default (Private) stannar precis lika osynlig för resten av
    /// hushållet som innan denna feature fanns: varken på Vecka eller via det nya
    /// household-endpointet.</summary>
    [Fact]
    public async Task A_private_reminder_is_invisible_to_another_member_on_vecka_and_via_the_household_endpoint()
    {
        var (annaPage, bjornPage, _, bjornHttp, householdId) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        // No visibilityButtonLabel - keeps the sheet's own default ("Bara jag" / Private).
        await CreateReminderViaUiAsync(annaPage, "Hemligt läkarbesök", today, "09:00");

        await bjornPage.GotoAsync("/vecka");
        await Assertions.Expect(bjornPage.GetByText("Hemligt läkarbesök")).Not.ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]")).ToHaveCountAsync(0);

        var response = await bjornHttp.GetAsync(
            $"/api/households/{householdId}/reminders/household?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, list.GetArrayLength());
    }

    /// <summary>BusyOnly - the other member sees that a time exists, never what it is. The
    /// assertion on the title is against the whole page, not just the reminder row, so a title
    /// that leaked anywhere else in the DOM would also be caught.</summary>
    [Fact]
    public async Task A_busy_only_reminder_shows_the_time_but_never_the_title_to_another_member()
    {
        var (annaPage, bjornPage, _, _, _) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        await CreateReminderViaUiAsync(
            annaPage, "Tandläkare", today, "10:00", visibilityButtonLabel: "Andra ser att jag har en tid");

        await bjornPage.GotoAsync("/vecka");
        var section = bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        await Assertions.Expect(section).ToContainTextAsync("10:00");
        await Assertions.Expect(section).ToContainTextAsync("har en tid");
        await Assertions.Expect(bjornPage.GetByText("Tandläkare")).Not.ToBeVisibleAsync();
    }

    /// <summary>Household - the other member sees the title and the time, but never the
    /// location (decision 2: Location never leaves the owner at any level), and the row is bare
    /// text - no "Bocka av"/"Ändra"/"Avboka" for a time someone else owns (decision 3: only the
    /// owner ever acts on their own reminder).</summary>
    [Fact]
    public async Task A_household_reminder_shows_title_and_time_but_never_the_location_or_action_buttons()
    {
        var (annaPage, bjornPage, _, _, _) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        await CreateReminderViaUiAsync(
            annaPage, "Föräldramöte", today, "14:00", location: "Skolan",
            visibilityButtonLabel: "Andra ser vad det är");

        await bjornPage.GotoAsync("/vecka");
        var section = bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        await Assertions.Expect(section).ToContainTextAsync("Föräldramöte");
        await Assertions.Expect(section).ToContainTextAsync("14:00");
        await Assertions.Expect(bjornPage.GetByText("Skolan")).Not.ToBeVisibleAsync();

        // Historiskt (steg 1) hävdade detta test "section.Locator("button")" med count 0 - inga
        // knappar av något slag på Annas rad. Steg 2 (docs/PRODUCT.md §11) lägger till "Lägg till
        // i mina påminnelser" på just den här sortens rad (Household, med titel), så den bredare
        // assertionen stämmer inte längre bokstavligt. Skillnaden är avsiktlig, inte en
        // försvagning: knappen gör ingenting med ANNAS påminnelse - den skapar en egen, fristående
        // kopia hos den som klickar (docs/ARCHITECTURE.md, Beslut: Synlighet för påminnelser).
        // Ägarens tid är fortfarande orörbar för alla utom ägaren, så assertionen smalnas av till
        // just de tre ägar-åtgärderna. Att raden inte har NÅGON ANNAN åtgärd än den nya knappen
        // täcks separat av A_household_reminder_row_has_no_action_for_another_member_besides_adding_it_to_their_own.
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Bocka av" })).ToHaveCountAsync(0);
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Ändra" })).ToHaveCountAsync(0);
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Avboka" })).ToHaveCountAsync(0);
    }

    /// <summary>Komplement till testet ovan (regel 5, docs/PRODUCT.md §11, steg 2) - den enda
    /// klickbara åtgärden på en annans delade rad är "Lägg till i mina påminnelser". Ingenting
    /// annat på raden är klickbart, inte bara de tre namngivna ägar-åtgärderna.</summary>
    [Fact]
    public async Task A_household_reminder_row_has_no_action_for_another_member_besides_adding_it_to_their_own()
    {
        var (annaPage, bjornPage, _, _, _) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        await CreateReminderViaUiAsync(
            annaPage, "Föräldramöte", today, "14:00", location: "Skolan",
            visibilityButtonLabel: "Andra ser vad det är");

        await bjornPage.GotoAsync("/vecka");
        var section = bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        var row = section.Locator("li", new() { HasText = "Föräldramöte" });

        await Assertions.Expect(row.Locator("button")).ToHaveCountAsync(1);
        await Assertions.Expect(
            row.GetByRole(AriaRole.Button, new() { Name = "Lägg till i mina påminnelser" })).ToBeVisibleAsync();
    }

    /// <summary>Regel 1 (docs/PRODUCT.md §11, steg 2) - en BusyOnly-rad har ingen titel att
    /// kopiera. "Anna har en tid" är inget man kan lägga till hos sig själv, så knappen visas
    /// inte alls på en sådan rad.</summary>
    [Fact]
    public async Task A_busy_only_reminder_row_has_no_add_to_my_reminders_button()
    {
        var (annaPage, bjornPage, _, _, _) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        await CreateReminderViaUiAsync(
            annaPage, "Tandläkare", today, "10:00", visibilityButtonLabel: "Andra ser att jag har en tid");

        await bjornPage.GotoAsync("/vecka");
        var section = bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        await Assertions.Expect(section).ToContainTextAsync("har en tid");
        await Assertions.Expect(
            section.GetByRole(AriaRole.Button, new() { Name = "Lägg till i mina påminnelser" })).ToHaveCountAsync(0);
    }

    /// <summary>Björns egentliga behov (steg 2, docs/PRODUCT.md §11): två personer till samma sak
    /// behöver var sin egen notis, eftersom notiser går per medlem. Mottagaren trycker, inte
    /// avsändaren - knappen skapar en egen, fristående kopia hos den som klickar, utan att röra
    /// ägarens tid alls (docs/ARCHITECTURE.md, Beslut: Synlighet för påminnelser). Kopian bär bara
    /// titel, datum och klockslag (regel 2) - aldrig plats eller restid.</summary>
    [Fact]
    public async Task Pressing_add_to_my_reminders_creates_an_independent_private_copy_for_the_pressing_member()
    {
        var (annaPage, bjornPage, _, _, _) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        await CreateReminderViaUiAsync(
            annaPage, "Föräldramöte", today, "14:00", location: "Skolan", travelMinutes: 20,
            visibilityButtonLabel: "Andra ser vad det är");

        await bjornPage.GotoAsync("/vecka");
        var othersSection = bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        var othersRow = othersSection.Locator("li", new() { HasText = "Föräldramöte" });
        await othersRow.GetByRole(AriaRole.Button, new() { Name = "Lägg till i mina påminnelser" }).ClickAsync();

        // Kvittot ÄR raden som dyker upp (regel 3) - ingen toast, ingen modal, ingen
        // bekräftelsedialog att vänta på här.
        var ownSection = bjornPage.Locator("ul[aria-label=\"Dina påminnelser den här veckan\"]");
        await Assertions.Expect(ownSection.GetByText("Föräldramöte")).ToBeVisibleAsync();
        await Assertions.Expect(ownSection).ToContainTextAsync("14:00");

        // Duplikatspärren (regel 4) - knappen är borta på Annas rad, ett andra tryck kan alltså
        // inte ge två kopior.
        await Assertions.Expect(
            othersSection.Locator("li", new() { HasText = "Föräldramöte" })
                .GetByRole(AriaRole.Button, new() { Name = "Lägg till i mina påminnelser" })).ToHaveCountAsync(0);

        // Kopian syns också på Björns Min dag, med hans EGNA ägar-åtgärder - och utan Annas plats
        // eller restid, som aldrig följde med (regel 2).
        await bjornPage.GotoAsync("/");
        var bjornReminders = bjornPage.Locator("ul[aria-label=\"Påminnelser\"]");
        var bjornRow = bjornReminders.Locator("li", new() { HasText = "Föräldramöte" });
        await Assertions.Expect(bjornRow).ToBeVisibleAsync();
        await Assertions.Expect(bjornRow).ToContainTextAsync("14:00");
        await Assertions.Expect(bjornRow.GetByText("Skolan")).ToHaveCountAsync(0);
        await Assertions.Expect(bjornRow.GetByText("Gå")).ToHaveCountAsync(0);
        await Assertions.Expect(bjornRow.GetByRole(AriaRole.Button, new() { Name = "Bocka av" })).ToBeVisibleAsync();
        await Assertions.Expect(bjornRow.GetByRole(AriaRole.Button, new() { Name = "Ändra" })).ToBeVisibleAsync();
        await Assertions.Expect(bjornRow.GetByRole(AriaRole.Button, new() { Name = "Avboka" })).ToBeVisibleAsync();

        // Annas egen påminnelse är oförändrad - hon ser fortfarande sin, med sin plats och sin
        // restid, och Björns tillägg har inte rört den.
        await annaPage.ReloadAsync();
        var annaReminders = annaPage.Locator("ul[aria-label=\"Påminnelser\"]");
        var annaRow = annaReminders.Locator("li", new() { HasText = "Föräldramöte" });
        await Assertions.Expect(annaRow).ToContainTextAsync("Skolan");
        await Assertions.Expect(annaRow).ToContainTextAsync("Gå 13:40");

        // Björn kan bocka av sin kopia utan att något händer med Annas.
        await bjornRow.GetByRole(AriaRole.Button, new() { Name = "Bocka av" }).ClickAsync();
        await Assertions.Expect(bjornPage.Locator("li.task-done", new() { HasText = "Föräldramöte" })).ToBeVisibleAsync();

        await annaPage.ReloadAsync();
        await Assertions.Expect(annaPage.Locator("li.task-done", new() { HasText = "Föräldramöte" })).ToHaveCountAsync(0);
        await Assertions.Expect(
            annaPage.Locator("ul[aria-label=\"Påminnelser\"]").GetByText("Föräldramöte")).ToBeVisibleAsync();
    }

    /// <summary>Sharing a reminder changes nothing about how the OWNER sees it - Min dag keeps its
    /// usual "Bocka av"/"Ändra"/"Avboka" row, and Vecka's own "Dina påminnelser den här veckan"
    /// still shows it exactly as before this feature existed.</summary>
    [Fact]
    public async Task The_owners_own_rows_on_min_dag_and_vecka_are_unchanged_by_sharing_a_reminder()
    {
        var (annaPage, _, _, _, _) = await ArrangeTwoMembersAsync();
        var today = AppDate.Today;

        await CreateReminderViaUiAsync(
            annaPage, "Tandläkare", today, "09:00", visibilityButtonLabel: "Andra ser vad det är");

        var reminderGroup = annaPage.Locator("ul[aria-label=\"Påminnelser\"]");
        await Assertions.Expect(reminderGroup.GetByText("Tandläkare")).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Bocka av" })).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Ändra" })).ToBeVisibleAsync();
        await Assertions.Expect(reminderGroup.GetByRole(AriaRole.Button, new() { Name = "Avboka" })).ToBeVisibleAsync();

        await annaPage.GotoAsync("/vecka");
        var own = annaPage.Locator("ul[aria-label=\"Dina påminnelser den här veckan\"]");
        await Assertions.Expect(own.GetByText("Tandläkare")).ToBeVisibleAsync();
    }

    /// <summary>The reminder-audience feature's own end-to-end proof (docs/ARCHITECTURE.md,
    /// "Beslut: Synlighet för påminnelser") - all four cases from the task in one flowing test,
    /// on the SAME shared reminder, so each transition is checked against the state the previous
    /// one actually left behind rather than a freshly created one:
    /// <list type="number">
    /// <item>Selected + Björn only -> Björn sees it on Vecka, Cecilia (the third member,
    /// deliberately never selected) does not - proving Selected actually narrows who sees it,
    /// not just "everyone but the owner" (two members alone could not tell those apart).</item>
    /// <item>Switched to "Alla i hushållet" -> both Björn and Cecilia see it.</item>
    /// <item>Switched back to "Bara utvalda" with nobody checked -> neither sees it - the
    /// fail-closed guarantee itself (docs/ARCHITECTURE.md), not "everyone" by omission.</item>
    /// <item>Anna's own Min dag/Vecka rows are unchanged throughout all three transitions -
    /// checked after each one, not just once at the end.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Sharing_a_reminder_with_selected_members_controls_who_sees_it_on_vecka()
    {
        var (annaPage, bjornPage, ceciliaPage, bjornName, _) = await ArrangeThreeMembersAsync();
        var today = AppDate.Today;

        static async Task AssertAnnasOwnViewIsUnchangedAsync(IPage annaPage)
        {
            var ownMinDag = annaPage.Locator("ul[aria-label=\"Påminnelser\"]");
            await Assertions.Expect(ownMinDag.GetByText("Föräldramöte")).ToBeVisibleAsync();
            await Assertions.Expect(ownMinDag.GetByRole(AriaRole.Button, new() { Name = "Bocka av" })).ToBeVisibleAsync();
            await Assertions.Expect(ownMinDag.GetByRole(AriaRole.Button, new() { Name = "Ändra" })).ToBeVisibleAsync();
            await Assertions.Expect(ownMinDag.GetByRole(AriaRole.Button, new() { Name = "Avboka" })).ToBeVisibleAsync();

            await annaPage.GotoAsync("/vecka");
            var ownVecka = annaPage.Locator("ul[aria-label=\"Dina påminnelser den här veckan\"]");
            await Assertions.Expect(ownVecka.GetByText("Föräldramöte")).ToBeVisibleAsync();
            await annaPage.GotoAsync("/");
        }

        // Fall 1: delad med bara Björn.
        await CreateReminderViaUiAsync(
            annaPage, "Föräldramöte", today, "18:00",
            visibilityButtonLabel: "Andra ser vad det är",
            audienceButtonLabel: "Bara utvalda",
            selectedMemberNames: [bjornName]);

        await bjornPage.GotoAsync("/vecka");
        var bjornSection = bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        await Assertions.Expect(bjornSection.GetByText("Föräldramöte")).ToBeVisibleAsync();

        await ceciliaPage.GotoAsync("/vecka");
        await Assertions.Expect(ceciliaPage.GetByText("Föräldramöte")).Not.ToBeVisibleAsync();
        await Assertions.Expect(ceciliaPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]")).ToHaveCountAsync(0);

        await AssertAnnasOwnViewIsUnchangedAsync(annaPage);

        // Fall 2: Anna byter till "Alla i hushållet" - båda ser den nu.
        await ChangeReminderAudienceViaUiAsync(annaPage, "Föräldramöte", "Alla i hushållet");

        await bjornPage.ReloadAsync();
        await Assertions.Expect(bjornSection.GetByText("Föräldramöte")).ToBeVisibleAsync();

        await ceciliaPage.ReloadAsync();
        var ceciliaSection = ceciliaPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]");
        await Assertions.Expect(ceciliaSection.GetByText("Föräldramöte")).ToBeVisibleAsync();

        await AssertAnnasOwnViewIsUnchangedAsync(annaPage);

        // Fall 3: Anna väljer "Bara utvalda" utan att kryssa någon - ingen av dem ser den.
        await ChangeReminderAudienceViaUiAsync(annaPage, "Föräldramöte", "Bara utvalda");

        await bjornPage.ReloadAsync();
        await Assertions.Expect(bjornPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]")).ToHaveCountAsync(0);

        await ceciliaPage.ReloadAsync();
        await Assertions.Expect(ceciliaPage.Locator("ul[aria-label=\"Andras tider den här veckan\"]")).ToHaveCountAsync(0);

        await AssertAnnasOwnViewIsUnchangedAsync(annaPage);
    }

    /// <summary>
    /// Locks the audience picker's own highlighting to the actual selection - "Alla i hushållet"
    /// and "Bara utvalda" use the same active/inactive button styling as the level-picker above
    /// them (btn-primary for the chosen one, btn-secondary for the other), and a screenshot alone
    /// cannot prove which one is genuinely active versus a transient :active/:focus style caught
    /// mid-click. This asserts the settled state after each click, both directions.
    /// </summary>
    [Fact]
    public async Task The_audience_picker_buttons_reflect_the_actual_selection()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta-" + Guid.NewGuid().ToString("N")[..6]);

        await page.GetByRole(AriaRole.Button, new() { Name = "Ny påminnelse" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Ny påminnelse" });
        await sheet.GetByLabel("Titel").FillAsync("Föräldramöte");
        await sheet.GetByLabel("Datum").FillAsync(AppDate.Today.ToString("yyyy-MM-dd"));
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Andra ser vad det är", Exact = true }).ClickAsync();

        var everyoneButton = sheet.GetByRole(AriaRole.Button, new() { Name = "Alla i hushållet", Exact = true });
        var selectedButton = sheet.GetByRole(AriaRole.Button, new() { Name = "Bara utvalda", Exact = true });

        // Default is "Everyone" - matches Reminder.Audience's own default (see commit 1).
        await Assertions.Expect(everyoneButton).ToHaveClassAsync(new Regex("btn-primary"));
        await Assertions.Expect(selectedButton).ToHaveClassAsync(new Regex("btn-secondary"));

        await selectedButton.ClickAsync();
        await Assertions.Expect(selectedButton).ToHaveClassAsync(new Regex("btn-primary"));
        await Assertions.Expect(everyoneButton).ToHaveClassAsync(new Regex("btn-secondary"));

        await everyoneButton.ClickAsync();
        await Assertions.Expect(everyoneButton).ToHaveClassAsync(new Regex("btn-primary"));
        await Assertions.Expect(selectedButton).ToHaveClassAsync(new Regex("btn-secondary"));
    }
}
