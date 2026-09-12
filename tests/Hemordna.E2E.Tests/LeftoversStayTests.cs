using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Kvarlämnat stannar" (docs/ARCHITECTURE.md, "Beslut: Kvarlämnat, Imorgon på Idag,
/// ledig dag och tid i förväg") - an occurrence already overdue never changes owner, not even
/// when "Balansera om vem som gör vad" is pressed by someone else in the household.</summary>
[Collection(HemordnaAppCollection.Name)]
public class LeftoversStayTests
{
    private readonly HemordnaAppFixture _app;

    public LeftoversStayTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task An_overdue_rotating_task_keeps_its_owner_after_someone_else_rebalances()
    {
        var aPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(aPage, "Astrid", "Familjen Kvarlämnat");

        await aPage.GotoAsync("/hushall");
        await aPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var code = await aPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" }).GetByLabel("Inbjudningskod").InnerTextAsync();
        await aPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" }).GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        var bPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(bPage, "Bosse");
        await bPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bPage.GetByLabel("Inbjudningskod").FillAsync(code);
        await bPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bPage.Locator("h1", new() { HasText = "Bosse" }).WaitForAsync(new() { Timeout = 15_000 });

        var aToken = await aPage.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var aHttp = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        aHttp.DefaultRequestHeaders.Authorization = new("Bearer", aToken);

        var aMe = await (await aHttp.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = aMe.GetProperty("householdId").GetGuid();
        var aMemberId = aMe.GetProperty("memberId").GetGuid();

        var bToken = await bPage.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var bHttp = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        bHttp.DefaultRequestHeaders.Authorization = new("Bearer", bToken);
        var bMemberId = (await (await bHttp.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("memberId").GetGuid();

        // The test's own point is that "who presses the button" does not matter to the
        // rebalance algorithm - not that a non-manager even can. "Balansera om vem som gör
        // vad" is household configuration (see docs/ARCHITECTURE.md "Beslut: Vem får ändra
        // vad"), so Bosse needs the ability before he can press it at all.
        await aHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{bMemberId}/can-manage",
            new { canManageHousehold = true });

        // Astrid: a small budget (20 min/day) - the same skew RebalanceTaskAssignmentsTests
        // uses to prove an overdue task would otherwise look like an attractive move: if the
        // "kvarlämnat" exclusion did not hold, this is exactly the setup where the ratio math
        // WOULD want to hand the overdue task to someone with more room.
        await aHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{aMemberId}/weekly-budget",
            new { monday = 20, tuesday = 20, wednesday = 20, thursday = 20, friday = 20, saturday = 20, sunday = 20 });

        var task = await (await aHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 15, hasRotatingResponsibility = true }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await aHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = yesterday, assignToMemberId = aMemberId });

        // Bosse - not Astrid - is the one who presses "Balansera om vem som gör vad".
        await bPage.GotoAsync("/hushall");
        await bPage.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" }).ClickAsync();
        var sheet = bPage.GetByRole(AriaRole.Dialog, new() { Name = "Balansera om vem som gör vad" });
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Balansera om vem som gör vad" }).ClickAsync();
        await Assertions.Expect(sheet.GetByRole(AriaRole.Status)).ToBeVisibleAsync();

        // Diska is still Astrid's, still overdue, still on HER Idag under "Sedan tidigare" -
        // never on Bosse's.
        await aPage.GotoAsync("/");
        await Assertions.Expect(aPage.GetByRole(AriaRole.List, new() { Name = "Sedan tidigare" }).GetByText("Diska"))
            .ToBeVisibleAsync();

        await bPage.GotoAsync("/");
        await Assertions.Expect(bPage.GetByText("Diska")).Not.ToBeVisibleAsync();
    }
}
