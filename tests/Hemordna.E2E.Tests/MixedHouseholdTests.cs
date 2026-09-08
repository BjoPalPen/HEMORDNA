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

    private static Task GiveFullWeekAsync(HttpClient http, Guid householdId, Guid memberId)
        => http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

    private static async Task<Guid> ScheduleTaskForTodayAsync(HttpClient http, Guid householdId, Guid memberId, string name)
    {
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name, estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();

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

        await GiveFullWeekAsync(aHttp, householdId, aMemberId);
        await GiveFullWeekAsync(bHttp, householdId, bMemberId);

        var aOccurrenceId = await ScheduleTaskForTodayAsync(aHttp, householdId, aMemberId, "Diska");
        await ScheduleTaskForTodayAsync(bHttp, householdId, bMemberId, "Dammsuga");

        // 1. A picks the "Steg för steg" preset and saves; B touches nothing.
        await aPage.GotoAsync("/installningar");
        await aPage.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();
        await aPage.GetByRole(AriaRole.Button, new() { Name = "Steg för steg" }).ClickAsync();
        var aSaveButton = aPage.GetByRole(AriaRole.Button, new() { Name = "Spara" });
        await aSaveButton.ClickAsync();
        await Assertions.Expect(aSaveButton).ToBeEnabledAsync();

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

        // 4. A's and B's Hushåll pages are identical apart from who is signed in - nothing on
        // this shared surface is personalised by login identity in the first place.
        await aPage.GotoAsync("/hushall");
        await aPage.GetByRole(AriaRole.Heading, new() { Name = "Familjen Blandad" }).WaitForAsync();
        var aHushallText = await aPage.Locator(".app-main").InnerTextAsync();
        Assert.Equal(aHushallText, bHushallText);

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
}
