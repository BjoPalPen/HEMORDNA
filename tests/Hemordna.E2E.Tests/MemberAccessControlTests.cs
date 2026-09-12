using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Steg 1 ("Bara jag ändrar mitt") - see docs/ARCHITECTURE.md "Beslut: Vem får ändra vad". A
/// member's personal settings (preferences, availability, day off, pause) may only be changed
/// by that member themselves, or by anyone in the household on behalf of a member who has no
/// account of their own and so can never sign in to set them.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class MemberAccessControlTests
{
    private readonly HemordnaAppFixture _app;

    public MemberAccessControlTests(HemordnaAppFixture app) => _app = app;

    private static async Task<HttpClient> AuthorizedHttpAsync(IPage page, string apiUrl)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    private static async Task<(Guid HouseholdId, Guid MemberId)> MeAsync(HttpClient http)
    {
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (me.GetProperty("householdId").GetGuid(), me.GetProperty("memberId").GetGuid());
    }

    private static async Task<string> ReadInviteCodeAsync(IPage page)
    {
        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        var code = dialog.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        var value = await code.InnerTextAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        return value;
    }

    /// <summary>Two account-holding members of the same household: Anna (creator, so she also
    /// starts with CanManageHousehold - irrelevant to this test file, only Steg 1 is exercised
    /// here) and Björn (joined via code).</summary>
    private async Task<(HttpClient AnnaHttp, Guid HouseholdId, Guid AnnaMemberId, Guid BjornMemberId)>
        ArrangeTwoAccountHoldersAsync()
    {
        var annaPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(annaPage, "Anna-" + Guid.NewGuid().ToString("N")[..6]);
        var inviteCode = await ReadInviteCodeAsync(annaPage);

        var bjornPage = await _app.NewPageAsync();
        var bjornName = "Björn-" + Guid.NewGuid().ToString("N")[..6];
        await SignUpHelper.RegisterAsync(bjornPage, bjornName);
        await bjornPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bjornPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bjornPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bjornPage.Locator("h1", new() { HasText = bjornName }).WaitForAsync(new() { Timeout = 15_000 });

        var annaHttp = await AuthorizedHttpAsync(annaPage, _app.ApiUrl);
        var bjornHttp = await AuthorizedHttpAsync(bjornPage, _app.ApiUrl);
        var (householdId, annaMemberId) = await MeAsync(annaHttp);
        var (_, bjornMemberId) = await MeAsync(bjornHttp);

        return (annaHttp, householdId, annaMemberId, bjornMemberId);
    }

    [Fact]
    public async Task A_member_cannot_read_another_account_holding_members_preferences()
    {
        var (annaHttp, householdId, _, bjornMemberId) = await ArrangeTwoAccountHoldersAsync();

        var response = await annaHttp.GetAsync(
            $"/api/households/{householdId}/members/{bjornMemberId}/preferences");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_member_cannot_change_another_account_holding_members_preferences()
    {
        var (annaHttp, householdId, _, bjornMemberId) = await ArrangeTwoAccountHoldersAsync();

        var response = await annaHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{bjornMemberId}/preferences",
            new { presentation = "Text", motivation = "None", showTimeLevel = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_member_cannot_change_another_account_holding_members_availability()
    {
        var (annaHttp, householdId, _, bjornMemberId) = await ArrangeTwoAccountHoldersAsync();
        var today = new DateOnly(2026, 4, 13);

        var response = await annaHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{bjornMemberId}/availability",
            new { date = today, availableMinutes = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_member_can_always_change_their_own_preferences()
    {
        var (annaHttp, householdId, annaMemberId, _) = await ArrangeTwoAccountHoldersAsync();

        var response = await annaHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{annaMemberId}/preferences",
            new { presentation = "Text", motivation = "None", showTimeLevel = true });

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_member_can_set_an_account_less_members_preferences()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia-" + Guid.NewGuid().ToString("N")[..6]);

        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Litet barn", "Barn eller ungdom");

        var http = await AuthorizedHttpAsync(page, _app.ApiUrl);
        var (householdId, _) = await MeAsync(http);
        var household = await (await http.GetAsync($"/api/households/{householdId}")).Content.ReadFromJsonAsync<JsonElement>();
        var childId = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Litet barn")
            .GetProperty("id").GetGuid();

        // The account-less member can never sign in to set this themselves - someone else in
        // the household must be able to, on their behalf.
        var response = await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{childId}/preferences",
            new { presentation = "LargeText", motivation = "Calm", showTimeLevel = false });

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_member_can_still_pause_another_account_holding_members_schedule()
    {
        // Deliberately NOT restricted to self-or-account-less, unlike the other personal
        // routes in this file: MemberSheet.razor already lets any member pause any other
        // member's own row (e.g. a partner travelling, marked paused on their behalf) - see
        // docs/ARCHITECTURE.md "Beslut: Vem får ändra vad" for why this one stayed as-is.
        var (annaHttp, householdId, _, bjornMemberId) = await ArrangeTwoAccountHoldersAsync();

        var response = await annaHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{bjornMemberId}/pause",
            new { until = new DateOnly(2026, 5, 1) });

        Assert.True(response.IsSuccessStatusCode);
    }
}
