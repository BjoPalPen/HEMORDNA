using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Del C ("Beslut: Ångra och stabil lista") - a household is normally MIXED: one member can run
/// "Steg för steg", another the plain default, a third nothing at all. Everything built in this
/// revision is either per-member (MemberPreference) or per-device (localStorage) - never a
/// Household-level field - and every shared surface (Hushåll, Vecka, Rum) stays neutral about
/// what any individual member has chosen. This test walks the five scenarios the revision's own
/// spec names, using the same invite-code join pattern as HouseholdInviteTests.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class MixedHouseholdTests
{
    private readonly HemordnaAppFixture _app;

    public MixedHouseholdTests(HemordnaAppFixture app) => _app = app;

    private static async Task<string> ReadInviteCodeAsync(IPage page)
    {
        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var code = page.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" }).GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var value = await code.InnerTextAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" }).GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        return value;
    }

    private static async Task<(Guid HouseholdId, Guid MemberId)> MeAsync(HttpClient http)
    {
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (me.GetProperty("householdId").GetGuid(), me.GetProperty("memberId").GetGuid());
    }

    private static async Task<HttpClient> AuthorizedHttpAsync(IPage page, string apiUrl)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    /// <summary>The Hushåll page's shared household data (rooms, members, activity), with the
    /// two parts that legitimately differ by CanManageHousehold - see the test's own remarks -
    /// stripped out first: the "Bjud in" avatar tile, and the settings list at the bottom.</summary>
    private static Task<string> HushallTextExcludingSettingsAsync(IPage page)
        => page.EvaluateAsync<string>(
            """
            () => {
                const clone = document.querySelector('.app-main').cloneNode(true);
                const settings = clone.querySelector('ul[aria-label="Hushållsinställningar"]');
                if (settings) { settings.remove(); }
                const bjudIn = [...clone.querySelectorAll('.member-avatar-btn')]
                    .find(b => b.textContent.includes('Bjud in'));
                if (bjudIn) { bjudIn.remove(); }
                return clone.textContent;
            }
            """);

    private static Task GiveFullWeekAsync(HttpClient http, Guid householdId, Guid memberId)
        => http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

    /// <summary>
    /// Schedules a task for today on the caller themselves. Goes through
    /// <c>tasks/extra</c> rather than the household-configuration <c>POST /tasks</c> (see
    /// docs/ARCHITECTURE.md "Beslut: Vem får ändra vad"), so this works for every member
    /// here, not just one who can manage the household - <paramref name="memberId"/> must be
    /// <paramref name="http"/>'s own caller.
    /// </summary>
    private static async Task<Guid> ScheduleTaskForTodayAsync(HttpClient http, Guid householdId, Guid memberId, string name)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/extra",
            new { name, estimatedMinutes = 5, today }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(memberId, occurrence.GetProperty("assignedMemberId").GetGuid());

        return occurrence.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_mixed_household_never_leaks_one_members_choices_or_undone_actions_to_another()
    {
        var aPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(aPage, "Astrid", "Familjen Blandad");
        var inviteCode = await ReadInviteCodeAsync(aPage);

        var bPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(bPage, "Bosse");
        await bPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bPage.Locator("h1", new() { HasText = "Bosse" }).WaitForAsync(new() { Timeout = 15_000 });

        using var aHttp = await AuthorizedHttpAsync(aPage, _app.ApiUrl);
        using var bHttp = await AuthorizedHttpAsync(bPage, _app.ApiUrl);
        var (householdId, aMemberId) = await MeAsync(aHttp);
        var (_, bMemberId) = await MeAsync(bHttp);

        // Weekly budget is household configuration (see docs/ARCHITECTURE.md "Beslut: Vem
        // får ändra vad") - Astrid, the household's creator and so its manager, sets it for
        // both herself and Bosse; a joiner like Bosse cannot set even his own.
        await GiveFullWeekAsync(aHttp, householdId, aMemberId);
        await GiveFullWeekAsync(aHttp, householdId, bMemberId);

        var aOccurrenceId = await ScheduleTaskForTodayAsync(aHttp, householdId, aMemberId, "Diska");
        await ScheduleTaskForTodayAsync(bHttp, householdId, bMemberId, "Dammsuga");

        // 1. A picks the "Steg för steg" preset - saves itself, no separate "Spara" step any
        // more (see docs/DESIGN.md, "Inställningar – Min visning"). B touches nothing.
        await aPage.GotoAsync("/installningar");
        await aPage.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await aPage.GetByRole(AriaRole.Button, new() { Name = "Steg för steg" }).ClickAsync();
        await Assertions.Expect(aPage.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });

        // "Steg för steg" applies calm screen to A's OWN device immediately, same as any other
        // per-device toggle (§B11) - turn it back off on A's device now, so step 5 below tests
        // what it actually claims to (does B's own toggle leak to A), not leftover state from
        // A's own, already-verified choice in step 1/2.
        await aPage.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter").UncheckAsync();
        await aPage.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-calm')");

        // 2. B's Idag and Inställningar show no trace of A's choice.
        await bPage.GotoAsync("/");
        await bPage.Locator("h1", new() { HasText = "Bosse" }).WaitForAsync();

        await Assertions.Expect(bPage.Locator(".chip-time")).Not.ToBeVisibleAsync();
        await Assertions.Expect(bPage.Locator(".day-encouragement")).Not.ToBeVisibleAsync();
        await Assertions.Expect(bPage.Locator(".focus-card")).Not.ToBeVisibleAsync();
        await Assertions.Expect(bPage.Locator(".task-list").First).ToBeVisibleAsync();
        Assert.False(await bPage.EvaluateAsync<bool>("() => document.documentElement.hasAttribute('data-calm')"));

        await bPage.GotoAsync("/installningar");
        await bPage.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await Assertions.Expect(bPage.GetByLabel("Text (standard) - kompakt lista")).ToBeCheckedAsync();
        await Assertions.Expect(bPage.GetByLabel("Ingen - bara fakta")).ToBeCheckedAsync();

        // 3. A completes a task, then undoes it within the 8s window (done directly against the
        // API - the UI side of undo is UndoTests' job, this checks what a SEPARATE member sees
        // afterward). It must vanish quietly: not shown as done, no trace of the word "ångra".
        await aHttp.PostAsync($"/api/households/{householdId}/occurrences/{aOccurrenceId}/complete", null);
        await aHttp.PostAsync($"/api/households/{householdId}/occurrences/{aOccurrenceId}/reopen", null);

        await bPage.GotoAsync("/");
        await bPage.Locator("h1", new() { HasText = "Bosse" }).WaitForAsync();
        var bIdagText = await bPage.Locator(".app-main").InnerTextAsync();
        Assert.DoesNotContain("Diska", bIdagText);
        Assert.DoesNotContain("ångra", bIdagText, StringComparison.OrdinalIgnoreCase);

        await bPage.GotoAsync("/hushall");
        await bPage.GetByRole(AriaRole.Heading, new() { Name = "Familjen Blandad" }).WaitForAsync();
        var bHushallText = await bPage.Locator(".app-main").InnerTextAsync();
        Assert.DoesNotContain("Diska", bHushallText);
        Assert.DoesNotContain("ångra", bHushallText, StringComparison.OrdinalIgnoreCase);

        // 4. A's and B's Hushåll pages show the same household data apart from who is signed
        // in - nothing about the ROOMS/MEMBERS/ACTIVITY this surface shows is personalised by
        // login identity. The settings list at the bottom is the one deliberate exception
        // (denna revision, se docs/ARCHITECTURE.md "Beslut: Vem får ändra vad"): Astrid
        // created the household and can manage it, Bosse joined by code and cannot, so only
        // her page offers "Bjud in" and the other configuration rows - excluded here, and
        // checked in its own right below.
        await aPage.GotoAsync("/hushall");
        await aPage.GetByRole(AriaRole.Heading, new() { Name = "Familjen Blandad" }).WaitForAsync();
        Assert.Equal(await HushallTextExcludingSettingsAsync(aPage), await HushallTextExcludingSettingsAsync(bPage));

        await Assertions.Expect(aPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(bPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in", Exact = true }))
            .Not.ToBeVisibleAsync();

        // 5. B turns on "Lugnare skärm" on her own device; A's separate browser context is
        // unaffected.
        await bPage.GotoAsync("/installningar");
        await bPage.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await bPage.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter").CheckAsync();
        await bPage.WaitForFunctionAsync("() => document.documentElement.hasAttribute('data-calm')");

        await aPage.GotoAsync("/hushall");
        await aPage.GetByRole(AriaRole.Heading, new() { Name = "Familjen Blandad" }).WaitForAsync();
        Assert.False(await aPage.EvaluateAsync<bool>("() => document.documentElement.hasAttribute('data-calm')"));
    }

    /// <summary>
    /// Del C/G's "neutral planning info" exception: unlike a completion or an undo, a member's
    /// own day off IS meant to be visible to the rest of the household - see Vecka.razor's own
    /// DotClass remarks. This is the flip side of the next test below, where time credit stays
    /// strictly private.
    /// </summary>
    [Fact]
    public async Task A_members_day_off_shows_to_the_rest_of_the_household_as_a_neutral_dot()
    {
        var aPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(aPage, "Kerstin", "Familjen Ledig");
        var inviteCode = await ReadInviteCodeAsync(aPage);

        var bPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(bPage, "Lennart");
        await bPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bPage.Locator("h1", new() { HasText = "Lennart" }).WaitForAsync(new() { Timeout = 15_000 });

        await aPage.GotoAsync("/");
        await aPage.GetByRole(AriaRole.Button, new() { Name = "Ta ledigt idag" }).ClickAsync();
        var sheet = aPage.GetByRole(AriaRole.Dialog, new() { Name = "Ledig dag" });
        await sheet.WaitForAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Ta med till idag" }).ClickAsync();
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Ångra ledig dag" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });

        // B never touched anything - this is A's day off appearing on B's OWN view of the
        // shared weekly grid, not a reflection of B's own state.
        await bPage.GotoAsync("/vecka");
        var kerstinRow = bPage.Locator("tr", new() { HasText = "Kerstin" });
        await Assertions.Expect(kerstinRow.Locator(".dot-off")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// The strict opposite of the day-off test above: "tid i förväg" is deliberately NEVER
    /// visible to anyone but the member it belongs to (see MemberTimeCredit's own remarks, and
    /// GetMemberTimeCredit's endpoint having no memberId route parameter to ask for someone
    /// else's). A completes an extra task and earns real credit; B's own page must show nothing
    /// about it at all.
    /// </summary>
    [Fact]
    public async Task A_members_time_credit_never_leaks_to_another_member()
    {
        var aPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(aPage, "Marta", "Familjen Privat");
        var inviteCode = await ReadInviteCodeAsync(aPage);

        var bPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(bPage, "Niklas");
        await bPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bPage.Locator("h1", new() { HasText = "Niklas" }).WaitForAsync(new() { Timeout = 15_000 });

        using var aHttp = await AuthorizedHttpAsync(aPage, _app.ApiUrl);
        using var bHttp = await AuthorizedHttpAsync(bPage, _app.ApiUrl);
        var (householdId, aMemberId) = await MeAsync(aHttp);
        var (_, bMemberId) = await MeAsync(bHttp);

        await GiveFullWeekAsync(aHttp, householdId, aMemberId);

        var task = await (await aHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Extra diskning", estimatedMinutes = 30 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var occurrence = await (await aHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = aMemberId, addedAsExtra = true }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await aHttp.PostAsync(
            $"/api/households/{householdId}/occurrences/{occurrence.GetProperty("id").GetGuid()}/complete", null);

        // A really does have credit now - proves the setup worked, not that the endpoint is
        // broken in a way that would make the leak check below meaningless.
        var aCredit = await (await aHttp.GetAsync($"/api/households/{householdId}/time-credit"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(30, aCredit.GetProperty("minutes").GetInt32());

        // B's own balance is genuinely zero, and B's own page must say nothing at all - not "0
        // min" (see GetMemberTimeCreditTests - the whole line is omitted below >0) and certainly
        // not A's 30.
        await bPage.GotoAsync("/vecka");
        await bPage.GetByRole(AriaRole.Heading, new() { Name = "Min vecka" }).WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(bPage.GetByText("Tid i förväg")).Not.ToBeVisibleAsync();

        // Belt and braces: the endpoint itself, called as B, returns B's own zero - never A's.
        var bCredit = await (await bHttp.GetAsync($"/api/households/{householdId}/time-credit"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, bCredit.GetProperty("minutes").GetInt32());
    }
}
