using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B2 - the remote-note shows the completer's real
/// name when they are a known household member, rather than always falling back to "Någon
/// annan" (previously a documented, reported contract gap: neither PlannedTaskResponse nor
/// CompletedTaskResponse carried who completed an occurrence - see ARCHITECTURE.md §B2).</summary>
[Collection(HemordnaAppCollection.Name)]
public class RemoteCompletionNameTests
{
    private readonly HemordnaAppFixture _app;

    public RemoteCompletionNameTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task A_real_household_members_name_appears_instead_of_someone_else()
    {
        var aPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(aPage, "Astrid", "Familjen Namn");

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
        var bToken = await bPage.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var bHttp = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        bHttp.DefaultRequestHeaders.Authorization = new("Bearer", bToken);

        var aMe = await (await aHttp.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = aMe.GetProperty("householdId").GetGuid();
        var aMemberId = aMe.GetProperty("memberId").GetGuid();

        await aHttp.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{aMemberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await aHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await aHttp.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = aMemberId }))
            .Content.ReadFromJsonAsync<JsonElement>();

        await aPage.GotoAsync("/");
        await Assertions.Expect(aPage.GetByRole(AriaRole.Button, new() { Name = "Markera Diska som klar" })).ToBeVisibleAsync();

        // Bosse (not Astrid) completes it - a genuinely different member, on her own device.
        await bHttp.PostAsync($"/api/households/{householdId}/occurrences/{occurrence.GetProperty("id").GetGuid()}/complete", null);

        var remoteNote = aPage.GetByRole(AriaRole.Status).Filter(new() { HasText = "bockade av Diska" });
        await Assertions.Expect(remoteNote).ToHaveTextAsync("Bosse bockade av Diska.");
    }
}
