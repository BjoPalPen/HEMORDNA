using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Steg 2 ("kan ändra hushållet") - see docs/ARCHITECTURE.md "Beslut: Vem får ändra vad".
/// <see cref="Hemordna.Domain.Households.HouseholdMember.CanManageHousehold"/> gates rooms,
/// tasks, members and household-wide settings; daily work and every GET stay open to all.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class HouseholdManageAccessTests
{
    private readonly HemordnaAppFixture _app;

    public HouseholdManageAccessTests(HemordnaAppFixture app) => _app = app;

    private static async Task<HttpClient> AuthorizedHttpAsync(IPage page, string apiUrl)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    private static async Task<JsonElement> MeAsync(HttpClient http)
        => await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();

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

    /// <summary>Anna creates the household (and so starts as its manager); Björn joins via code
    /// (and so does not) - the meaningful default from docs/ARCHITECTURE.md "Beslut: Vem får
    /// ändra vad".</summary>
    private async Task<(HttpClient AnnaHttp, HttpClient BjornHttp, Guid HouseholdId, Guid AnnaMemberId, Guid BjornMemberId)>
        ArrangeCreatorAndJoinerAsync()
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
        var annaMe = await MeAsync(annaHttp);
        var bjornMe = await MeAsync(bjornHttp);

        return (
            annaHttp, bjornHttp,
            annaMe.GetProperty("householdId").GetGuid(),
            annaMe.GetProperty("memberId").GetGuid(),
            bjornMe.GetProperty("memberId").GetGuid());
    }

    [Fact]
    public async Task The_creator_can_manage_the_household_and_the_joiner_cannot()
    {
        var (annaHttp, bjornHttp, _, _, _) = await ArrangeCreatorAndJoinerAsync();

        var annaMe = await MeAsync(annaHttp);
        var bjornMe = await MeAsync(bjornHttp);

        Assert.True(annaMe.GetProperty("canManageHousehold").GetBoolean());
        Assert.False(bjornMe.GetProperty("canManageHousehold").GetBoolean());
    }

    [Fact]
    public async Task Without_the_flag_a_locked_endpoint_is_forbidden()
    {
        var (_, bjornHttp, householdId, _, _) = await ArrangeCreatorAndJoinerAsync();

        var response = await bjornHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Ett nytt rum" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_the_flag_the_same_endpoint_works()
    {
        var (annaHttp, _, householdId, _, _) = await ArrangeCreatorAndJoinerAsync();

        var response = await annaHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Ett nytt rum" });

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Granting_the_flag_lets_the_recipient_use_locked_endpoints()
    {
        var (annaHttp, bjornHttp, householdId, _, bjornMemberId) = await ArrangeCreatorAndJoinerAsync();

        var grant = await annaHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{bjornMemberId}/can-manage",
            new { canManageHousehold = true });
        Assert.True(grant.IsSuccessStatusCode);

        var response = await bjornHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Björns rum" });

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task The_households_last_manager_cannot_have_the_flag_removed()
    {
        var (annaHttp, _, householdId, annaMemberId, _) = await ArrangeCreatorAndJoinerAsync();

        var response = await annaHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{annaMemberId}/can-manage",
            new { canManageHousehold = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_households_last_manager_cannot_be_deactivated()
    {
        var (annaHttp, _, householdId, annaMemberId, _) = await ArrangeCreatorAndJoinerAsync();

        var response = await annaHttp.DeleteAsync($"/api/households/{householdId}/members/{annaMemberId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_account_less_member_cannot_be_granted_the_flag()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Cecilia-" + Guid.NewGuid().ToString("N")[..6]);
        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Litet barn", "Barn eller ungdom");

        var http = await AuthorizedHttpAsync(page, _app.ApiUrl);
        var me = await MeAsync(http);
        var householdId = me.GetProperty("householdId").GetGuid();
        var household = await (await http.GetAsync($"/api/households/{householdId}")).Content.ReadFromJsonAsync<JsonElement>();
        var childId = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Litet barn")
            .GetProperty("id").GetGuid();

        var response = await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{childId}/can-manage",
            new { canManageHousehold = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Configuration_surfaces_are_hidden_from_a_member_without_the_flag()
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

        // A positive wait FIRST, on each page/sheet, so the negative checks below can never pass
        // vacuously just because Blazor has not finished rendering yet (an absent element reads
        // identically whether it is correctly hidden or simply not loaded yet) - see
        // RoomTile's "Övrigt" and the household heading as the "this has actually rendered" signal.
        await bjornPage.GotoAsync("/rum");
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Övrigt" })).ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Nytt rum", Exact = true }))
            .Not.ToBeVisibleAsync();

        await bjornPage.GotoAsync("/hushall");
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = bjornName })).ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in", Exact = true }))
            .Not.ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Pausa hushållet", Exact = true }))
            .Not.ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad", Exact = true }))
            .Not.ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Rensa hushållets data", Exact = true }))
            .Not.ToBeVisibleAsync();

        // Daily work and the rest of the page stay reachable.
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Link, new() { Name = "Inställningar", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(bjornPage.GetByRole(AriaRole.Button, new() { Name = "Logga ut", Exact = true }))
            .ToBeVisibleAsync();

        // A member's own sheet keeps its pause control (Steg 1, not Steg 2) but loses the
        // configuration rows - role, custom time, remove. The pause field's own wait is the
        // "this has actually rendered" signal for the negative checks that follow it.
        var sheet = await HushallHelper.OpenMemberSheetAsync(bjornPage, bjornName);
        await Assertions.Expect(sheet.GetByLabel("Pausa till och med")).ToBeVisibleAsync();
        await Assertions.Expect(sheet.GetByText("Vilken roll har personen i hushållet?")).Not.ToBeVisibleAsync();
        await Assertions.Expect(sheet.GetByText("Anpassad tid i stället")).Not.ToBeVisibleAsync();
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Ta bort medlem", Exact = true }))
            .Not.ToBeVisibleAsync();
    }
}
